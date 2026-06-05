import os
import json
import sqlite3
import urllib.request
import urllib.error
import signal
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse, parse_qs
from datetime import datetime

PORT = int(os.environ.get("INTERFACE_PORT", "8000"))
STATIC_DIR = os.path.join(os.path.dirname(os.path.abspath(__file__)), "static")
DB_PATH = os.environ.get("DB_PATH") or os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "dados", "sistemas_distribuidos.db"))
RPC_URL = os.environ.get("ANALISE_RPC_URL", "http://127.0.0.1:6001")

MIME_TYPES = {
    ".html": "text/html",
    ".css": "text/css",
    ".js": "application/javascript",
    ".png": "image/png",
    ".jpg": "image/jpeg",
    ".ico": "image/x-icon",
    ".json": "application/json",
}

class DashboardHandler(BaseHTTPRequestHandler):
    def _responder_json(self, data, status=200):
        self.send_response(status)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()
        self.wfile.write(json.dumps(data, ensure_ascii=False).encode("utf-8"))

    def _responder_erro(self, mensagem, status=400):
        self._responder_json({"sucesso": False, "erro": mensagem}, status)

    def do_OPTIONS(self):
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.end_headers()

    def do_GET(self):
        parsed_url = urlparse(self.path)
        path = parsed_url.path
        query = parse_qs(parsed_url.query)

        # Roteamento da API
        if path == "/health":
            self._responder_json({"status": "ok"})
        elif path == "/api/sensores":
            self._listar_sensores()
        elif path == "/api/tipos":
            self._listar_tipos_dados()
        elif path == "/api/medicoes":
            self._listar_medicoes(query)
        elif path == "/api/analises":
            self._listar_analises()
        else:
            # Servir arquivos estáticos
            self._servir_estatico(path)

    def do_POST(self):
        parsed_url = urlparse(self.path)
        path = parsed_url.path

        if path == "/api/analise":
            content_length = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(content_length) if content_length > 0 else b"{}"
            try:
                dados = json.loads(body.decode("utf-8"))
                self._executar_analise(dados)
            except json.JSONDecodeError:
                self._responder_erro("JSON inválido no corpo da requisição")
            except Exception as e:
                self._responder_erro(f"Erro inesperado: {str(e)}", 500)
        else:
            self._responder_erro(f"Rota POST não encontrada: {path}", 404)

    def _listar_sensores(self):
        try:
            if not os.path.exists(DB_PATH):
                self._responder_json([])
                return
            
            conn = sqlite3.connect(DB_PATH)
            cursor = conn.cursor()
            cursor.execute("SELECT DISTINCT sensor_id FROM medicoes ORDER BY sensor_id ASC;")
            sensores = [row[0] for row in cursor.fetchall()]
            conn.close()
            self._responder_json(sensores)
        except Exception as e:
            self._responder_erro(f"Erro ao consultar sensores: {str(e)}", 500)

    def _listar_tipos_dados(self):
        try:
            if not os.path.exists(DB_PATH):
                # Retorna os tipos padrão se o banco de dados não existir
                self._responder_json(["temperatura", "humidade", "qualidade_ar", "ruido", "pm25", "pm10", "luminosidade", "imagem"])
                return
            
            conn = sqlite3.connect(DB_PATH)
            cursor = conn.cursor()
            cursor.execute("SELECT DISTINCT tipo_dado FROM medicoes ORDER BY tipo_dado ASC;")
            tipos = [row[0] for row in cursor.fetchall()]
            conn.close()

            if not tipos:
                tipos = ["temperatura", "humidade", "qualidade_ar", "ruido", "pm25", "pm10", "luminosidade", "imagem"]
            self._responder_json(tipos)
        except Exception as e:
            self._responder_erro(f"Erro ao consultar tipos de dados: {str(e)}", 500)

    def _listar_medicoes(self, query):
        try:
            if not os.path.exists(DB_PATH):
                self._responder_json([])
                return

            tipo_dado = query.get("tipo_dado", [None])[0]
            sensor_id = query.get("sensor_id", [None])[0]
            
            limit_str = query.get("limit", ["100"])[0]
            try:
                limit = int(limit_str)
            except ValueError:
                limit = 100

            conn = sqlite3.connect(DB_PATH)
            cursor = conn.cursor()

            sql = "SELECT id, sensor_id, tipo_dado, valor, timestamp, payload_json FROM medicoes WHERE 1=1"
            params = []

            if tipo_dado:
                sql += " AND tipo_dado = ?"
                params.append(tipo_dado)
            if sensor_id:
                sql += " AND sensor_id = ?"
                params.append(sensor_id)

            sql += " ORDER BY id DESC LIMIT ?"
            params.append(limit)

            cursor.execute(sql, params)
            rows = cursor.fetchall()
            conn.close()

            medicoes = []
            for row in rows:
                payload_json_raw = row[5] if len(row) > 5 else None
                payload = None
                if payload_json_raw:
                    try:
                        payload = json.loads(payload_json_raw)
                    except Exception:
                        payload = payload_json_raw

                medicoes.append({
                    "id": row[0],
                    "sensor_id": row[1],
                    "tipo_dado": row[2],
                    "valor": row[3],
                    "timestamp": row[4],
                    "payload": payload
                })

            self._responder_json(medicoes)
        except Exception as e:
            self._responder_erro(f"Erro ao consultar medições: {str(e)}", 500)

    def _listar_analises(self):
        try:
            if not os.path.exists(DB_PATH):
                self._responder_json([])
                return

            conn = sqlite3.connect(DB_PATH)
            cursor = conn.cursor()
            cursor.execute("SELECT id, sensor_id, tipo_dado, tipo_analise, resultado, timestamp FROM analises ORDER BY id DESC LIMIT 50;")
            rows = cursor.fetchall()
            conn.close()

            analises = []
            for row in rows:
                resultado_raw = row[4]
                try:
                    resultado_parsed = json.loads(resultado_raw)
                except Exception:
                    resultado_parsed = resultado_raw

                analises.append({
                    "id": row[0],
                    "sensor_id": row[1],
                    "tipo_dado": row[2],
                    "tipo_analise": row[3],
                    "resultado": resultado_parsed,
                    "timestamp": row[5]
                })

            self._responder_json(analises)
        except Exception as e:
            self._responder_erro(f"Erro ao consultar análises: {str(e)}", 500)

    def _executar_analise(self, dados):
        sensor_id = dados.get("sensor_id")
        tipo_dado = dados.get("tipo_dado")
        tipo_analise = dados.get("tipo_analise") # "estatisticas", "padroes", "previsao"

        if not sensor_id or not tipo_dado or not tipo_analise:
            self._responder_erro("Campos obrigatórios em falta: 'sensor_id', 'tipo_dado', 'tipo_analise'")
            return

        if tipo_dado == "imagem":
            self._responder_erro("O tipo de dado 'imagem' não suporta análise estatística numérica. Selecione um tipo numérico (temperatura, humidade, etc.).")
            return

        if tipo_analise not in ["estatisticas", "padroes", "previsao"]:
            self._responder_erro("Tipo de análise inválido. Deve ser 'estatisticas', 'padroes' ou 'previsao'")
            return

        try:
            if not os.path.exists(DB_PATH):
                self._responder_erro("Não existem dados recolhidos para efetuar análises.")
                return

            # Obter medições para a análise
            conn = sqlite3.connect(DB_PATH)
            cursor = conn.cursor()
            
            sql = "SELECT valor FROM medicoes WHERE tipo_dado = ?"
            params = [tipo_dado]
            if sensor_id != "todos":
                sql += " AND sensor_id = ?"
                params.append(sensor_id)
            
            sql += " ORDER BY id DESC LIMIT 1000;"
            cursor.execute(sql, params)
            rows = cursor.fetchall()
            conn.close()

            if not rows:
                self._responder_erro(f"Sem dados suficientes de '{tipo_dado}' para o sensor '{sensor_id}'")
                return

            # Converter para lista de valores (últimos primeiro nas queries, mas para o RPC precisamos de ordem cronológica)
            valores = [float(row[0]) for row in rows]
            valores.reverse() # Ordem cronológica

            # Encaminhar para o serviço RPC
            rpc_endpoint = f"{RPC_URL}/rpc/{tipo_analise}"
            payload = {
                "sensor_id": sensor_id,
                "tipo_dado": tipo_dado,
                "valores": valores
            }
            
            req = urllib.request.Request(
                rpc_endpoint,
                data=json.dumps(payload).encode("utf-8"),
                headers={"Content-Type": "application/json"}
            )
            
            try:
                with urllib.request.urlopen(req, timeout=10) as response:
                    rpc_res = json.loads(response.read().decode("utf-8"))
            except urllib.error.URLError as rpc_err:
                self._responder_erro(f"Erro ao contactar o serviço RPC de Análise (Porto 6001): {str(rpc_err)}")
                return

            if not rpc_res.get("sucesso", False):
                self._responder_erro(f"Serviço RPC retornou erro: {rpc_res.get('erro', 'Desconhecido')}")
                return

            # Persistir análise realizada
            conn = sqlite3.connect(DB_PATH)
            cursor = conn.cursor()
            cursor.execute(
                "INSERT INTO analises (sensor_id, tipo_dado, tipo_analise, resultado, timestamp) VALUES (?, ?, ?, ?, ?);",
                (sensor_id, tipo_dado, tipo_analise, json.dumps(rpc_res), datetime.utcnow().isoformat())
            )
            conn.commit()
            conn.close()

            self._responder_json(rpc_res)

        except Exception as e:
            self._responder_erro(f"Erro no processamento da análise: {str(e)}", 500)

    def _servir_estatico(self, path):
        if path == "/":
            path = "/index.html"

        # Remover '/' inicial para construir caminho local
        file_path = os.path.abspath(os.path.join(STATIC_DIR, path.lstrip("/")))

        # Segurança: garantir que o arquivo reside dentro do STATIC_DIR
        if not file_path.startswith(STATIC_DIR):
            self.send_response(403)
            self.end_headers()
            self.wfile.write(b"Acesso negado")
            return

        if not os.path.exists(file_path) or os.path.isdir(file_path):
            self.send_response(404)
            self.end_headers()
            self.wfile.write(b"Arquivo nao encontrado")
            return

        # Obter extensão e content-type correspondente
        ext = os.path.splitext(file_path)[1].lower()
        content_type = MIME_TYPES.get(ext, "application/octet-stream")

        try:
            with open(file_path, "rb") as f:
                content = f.read()

            self.send_response(200)
            self.send_header("Content-Type", content_type)
            self.send_header("Content-Length", str(len(content)))
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(content)
        except Exception as e:
            self.send_response(500)
            self.end_headers()
            self.wfile.write(f"Erro ao ler arquivo: {str(e)}".encode("utf-8"))

def iniciar_servidor():
    os.makedirs(STATIC_DIR, exist_ok=True)
    server = HTTPServer(("0.0.0.0", PORT), DashboardHandler)
    print(f"┌──────────────────────────────────────────────────────────────┐")
    print(f"│      PAINEL DE VISUALIZAÇÃO INTERATIVO (DASHBOARD)            │")
    print(f"├──────────────────────────────────────────────────────────────┤")
    print(f"│  Porto: {PORT}")
    print(f"│  API Base: http://127.0.0.1:{PORT}/api")
    print(f"│  Servidor Estático: {STATIC_DIR}")
    print(f"│  Base de dados: {DB_PATH}")
    print(f"└──────────────────────────────────────────────────────────────┘")

    def sinal_paragem(sig, frame):
        print("\nPainel de visualização terminado.")
        server.server_close()

    signal.signal(signal.SIGTERM, sinal_paragem)
    signal.signal(signal.SIGINT, sinal_paragem)

    try:
        server.serve_forever()
    except KeyboardInterrupt:
        sinal_paragem(None, None)

if __name__ == "__main__":
    iniciar_servidor()
