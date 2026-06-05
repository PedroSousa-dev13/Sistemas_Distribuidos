import json
import random
import time
import threading
import signal
import sys
from datetime import datetime, timezone

try:
    import pika
except ImportError:
    print("A instalar pika...")
    import subprocess
    subprocess.check_call([sys.executable, "-m", "pip", "install", "pika"])
    import pika

RABBITMQ_HOST = "localhost"
RABBITMQ_PORT = 5672
EXCHANGE_MEASUREMENTS = "sensor-measurements"
EXCHANGE_CONTROL = "sensor-control"

SENSORES = [
    {"id": "sensor-temperatura-01", "zona": "A", "tipos": ["temperatura", "humidade"]},
    {"id": "sensor-qualidade-01", "zona": "B", "tipos": ["qualidade_ar", "pm25", "pm10"]},
    {"id": "sensor-ruido-01", "zona": "A", "tipos": ["ruido"]},
    {"id": "sensor-luz-01",  "zona": "C", "tipos": ["luminosidade"]},
    {"id": "sensor-ambiente-01", "zona": "B", "tipos": ["temperatura", "humidade", "qualidade_ar"]},
    {"id": "sensor-particulas-01", "zona": "C", "tipos": ["pm25", "pm10", "ruido"]},
]

def gerar_valor(tipo_dado):
    valores = {
        "temperatura":  lambda: round(random.uniform(15, 35), 1),
        "humidade":     lambda: round(random.uniform(30, 90), 1),
        "qualidade_ar": lambda: round(random.uniform(0, 200), 0),
        "ruido":        lambda: round(random.uniform(30, 100), 1),
        "pm25":         lambda: round(random.uniform(0, 100), 1),
        "pm10":         lambda: round(random.uniform(0, 180), 1),
        "luminosidade": lambda: round(random.uniform(0, 1200), 0),
    }
    return valores.get(tipo_dado, lambda: round(random.uniform(0, 100), 1))()

def criar_mensagem(sensor_id, tipo_dado, valor):
    return {
        "tipo": "DATA",
        "sensor_id": sensor_id,
        "payload": {
            "tipo_dado": tipo_dado,
            "valor": valor
        },
        "timestamp": datetime.now(timezone.utc).isoformat()
    }

def criar_registo(sensor_id, tipos):
    return {
        "tipo": "REGISTER",
        "sensor_id": sensor_id,
        "payload": {"tipos_dados": tipos},
        "timestamp": datetime.now(timezone.utc).isoformat()
    }

def sensor_worker(sensor, interval, stop_event):
    try:
        conn = pika.BlockingConnection(pika.ConnectionParameters(
            host=RABBITMQ_HOST, port=RABBITMQ_PORT,
            heartbeat=30
        ))
        channel = conn.channel()

        channel.exchange_declare(exchange=EXCHANGE_MEASUREMENTS, exchange_type="topic", durable=True)
        channel.exchange_declare(exchange=EXCHANGE_CONTROL, exchange_type="direct", durable=True)

        # Registar sensor
        msg_reg = criar_registo(sensor["id"], sensor["tipos"])
        channel.basic_publish(
            exchange=EXCHANGE_CONTROL,
            routing_key="register",
            body=json.dumps(msg_reg, ensure_ascii=False).encode("utf-8"),
            properties=pika.BasicProperties(
                content_type="application/json",
                delivery_mode=2
            )
        )
        ts = datetime.now().strftime("%H:%M:%S")
        print(f"[{ts}] [REGISTO] {sensor['id']} -> {', '.join(sensor['tipos'])}")

        while not stop_event.is_set():
            for tipo in sensor["tipos"]:
                valor = gerar_valor(tipo)
                msg = criar_mensagem(sensor["id"], tipo, valor)
                routing_key = f"sensor.{sensor['id']}.{tipo}"
                channel.basic_publish(
                    exchange=EXCHANGE_MEASUREMENTS,
                    routing_key=routing_key,
                    body=json.dumps(msg, ensure_ascii=False).encode("utf-8"),
                    properties=pika.BasicProperties(
                        content_type="application/json",
                        delivery_mode=2
                    )
                )
                ts = datetime.now().strftime("%H:%M:%S")
                print(f"[{ts}] [{sensor['id']}] {tipo} = {valor}", flush=True)
                time.sleep(interval)

        conn.close()
    except Exception as e:
        ts = datetime.now().strftime("%H:%M:%S")
        print(f"[{ts}] [ERRO] {sensor['id']}: {e}")

def main():
    print("=" * 60)
    print("  SIMULADOR DE SENSORES - Sistemas Distribuidos")
    print(f"  RabbitMQ: {RABBITMQ_HOST}:{RABBITMQ_PORT}")
    print(f"  Sensores: {len(SENSORES)}")
    for s in SENSORES:
        print(f"    - {s['id']} (zona {s['zona']}): {', '.join(s['tipos'])}")
    print("=" * 60)
    print("  Ctrl+C para parar")
    print()

    interval = 3.0
    if len(sys.argv) > 1:
        try:
            interval = float(sys.argv[1])
        except ValueError:
            pass

    stop_event = threading.Event()
    threads = []

    def signal_handler(sig, frame):
        print("\nA parar sensores...")
        stop_event.set()
        for t in threads:
            t.join(timeout=2)
        print("Sensores parados.")
        sys.exit(0)

    signal.signal(signal.SIGINT, signal_handler)
    signal.signal(signal.SIGTERM, signal_handler)

    for sensor in SENSORES:
        t = threading.Thread(target=sensor_worker, args=(sensor, interval, stop_event), daemon=True)
        t.start()
        threads.append(t)

    try:
        while not stop_event.is_set():
            time.sleep(1)
    except KeyboardInterrupt:
        signal_handler(None, None)

if __name__ == "__main__":
    main()
