import json
import math
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse

PORT = 5001

LIMITES = {
    "temperatura": {"min": -50, "max": 100, "unidade": "celsius"},
    "humidade": {"min": 0, "max": 100, "unidade": "percentagem"},
    "pm25": {"min": 0, "max": 1000, "unidade": "ug/m3"},
    "pm10": {"min": 0, "max": 2000, "unidade": "ug/m3"},
    "qualidade_ar": {"min": 0, "max": 500, "unidade": "AQI"},
    "ruido": {"min": 0, "max": 200, "unidade": "dB"},
    "luminosidade": {"min": 0, "max": 200000, "unidade": "lux"},
    "imagem": {"min": 0, "max": 1, "unidade": "metadata"},
}

def uniformizar_temperatura(valor, formato_original):
    if formato_original and formato_original.upper() == "FAHRENHEIT":
        return (valor - 32) * 5.0 / 9.0
    if formato_original and formato_original.upper() == "KELVIN":
        return valor - 273.15
    return valor

def uniformizar_humidade(valor, formato_original):
    if formato_original and formato_original.upper() == "FRACAO":
        return valor * 100
    return valor

def uniformizar_valor(tipo_dado, valor, formato_original):
    if tipo_dado == "temperatura":
        return round(uniformizar_temperatura(valor, formato_original), 2)
    if tipo_dado == "humidade":
        return round(uniformizar_humidade(valor, formato_original), 2)
    return round(float(valor), 2)

def validar_valor(tipo_dado, valor):
    if tipo_dado not in LIMITES:
        return True, []
    limite = LIMITES[tipo_dado]
    erros = []
    if valor < limite["min"]:
        erros.append(f"Valor {valor} abaixo do minimo {limite['min']} para {tipo_dado}")
    if valor > limite["max"]:
        erros.append(f"Valor {valor} acima do maximo {limite['max']} para {tipo_dado}")
    return len(erros) == 0, erros

class RPCHandler(BaseHTTPRequestHandler):
    def _responder(self, data, status=200):
        self.send_response(status)
        self.send_header("Content-Type", "application/json")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(json.dumps(data, ensure_ascii=False).encode("utf-8"))

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_POST(self):
        path = urlparse(self.path).path
        content_length = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(content_length) if content_length > 0 else b"{}"

        try:
            dados = json.loads(body)
        except json.JSONDecodeError:
            self._responder({"sucesso": False, "erro": "JSON invalido"}, 400)
            return

        if path == "/rpc/uniformizar":
            self._uniformizar(dados)
        elif path == "/rpc/validar":
            self._validar(dados)
        else:
            self._responder({"sucesso": False, "erro": f"Metodo nao encontrado: {path}"}, 404)

    def _uniformizar(self, dados):
        sensor_id = dados.get("sensor_id", "")
        tipo_dado = dados.get("tipo_dado", "")
        timestamp = dados.get("timestamp", "")

        try:
            valor = float(dados.get("valor", 0))
        except (ValueError, TypeError):
            self._responder({"sucesso": False, "erro": "Valor invalido"}, 400)
            return

        formato_original = dados.get("formato_original", "JSON")
        unidade = LIMITES.get(tipo_dado, {}).get("unidade", "desconhecida")

        valor_uniformizado = uniformizar_valor(tipo_dado, valor, formato_original)

        resposta = {
            "sucesso": True,
            "sensor_id": sensor_id,
            "tipo_dado": tipo_dado,
            "valor_uniformizado": valor_uniformizado,
            "timestamp": timestamp,
            "unidade": unidade,
            "formato_original": formato_original,
        }
        self._responder(resposta)

    def _validar(self, dados):
        sensor_id = dados.get("sensor_id", "")
        tipo_dado = dados.get("tipo_dado", "")

        try:
            valor = float(dados.get("valor", 0))
        except (ValueError, TypeError):
            self._responder({
                "valido": False, "erros": ["Valor invalido"],
                "sensor_id": sensor_id, "tipo_dado": tipo_dado
            })
            return

        valido, erros = validar_valor(tipo_dado, valor)

        self._responder({
            "valido": valido,
            "erros": erros,
            "sensor_id": sensor_id,
            "tipo_dado": tipo_dado,
        })

    def log_message(self, format, *args):
        print(f"[PreProcessamento] {args[0]} {args[1]} {args[2]}")

if __name__ == "__main__":
    server = HTTPServer(("0.0.0.0", PORT), RPCHandler)
    print(f"┌──────────────────────────────────────────────────────────────┐")
    print(f"│          SERVICO DE PRE-PROCESSAMENTO (RPC)                 │")
    print(f"├──────────────────────────────────────────────────────────────┤")
    print(f"│  Porto: {PORT}")
    print(f"│  Metodos: /rpc/uniformizar, /rpc/validar")
    print(f"│  Formatos suportados: JSON, XML, CSV, Fahrenheit, Kelvin    │")
    print(f"└──────────────────────────────────────────────────────────────┘")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nServico de Pre-Processamento terminado.")
        server.server_close()
