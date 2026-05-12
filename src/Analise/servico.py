import json
from http.server import HTTPServer, BaseHTTPRequestHandler
from urllib.parse import urlparse

from analise_estatistica import calcular_estatisticas, detetar_anomalias, prever_proximo

PORT = 6001

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

        if path == "/rpc/estatisticas":
            self._estatisticas(dados)
        elif path == "/rpc/padroes":
            self._padroes(dados)
        elif path == "/rpc/previsao":
            self._previsao(dados)
        else:
            self._responder({"sucesso": False, "erro": f"Metodo nao encontrado: {path}"}, 404)

    def _estatisticas(self, dados):
        sensor_id = dados.get("sensor_id", "")
        tipo_dado = dados.get("tipo_dado", "")
        valores = dados.get("valores", [])

        if not valores:
            self._responder({"sucesso": False, "erro": "Lista de valores vazia"})
            return

        valores_num = []
        for v in valores:
            try:
                valores_num.append(float(v))
            except (ValueError, TypeError):
                continue

        if not valores_num:
            self._responder({"sucesso": False, "erro": "Nenhum valor numerico valido"})
            return

        resultado = calcular_estatisticas(valores_num)
        if resultado is None:
            self._responder({"sucesso": False, "erro": "Erro ao calcular estatisticas"})
            return

        resultado["sensor_id"] = sensor_id
        resultado["tipo_dado"] = tipo_dado
        resultado["sucesso"] = True
        self._responder(resultado)

    def _padroes(self, dados):
        sensor_id = dados.get("sensor_id", "")
        tipo_dado = dados.get("tipo_dado", "")
        valores = dados.get("valores", [])

        if not valores:
            self._responder({"sucesso": False, "erro": "Lista de valores vazia"})
            return

        valores_num = []
        for v in valores:
            try:
                valores_num.append(float(v))
            except (ValueError, TypeError):
                continue

        anomalias = detetar_anomalias(valores_num)

        _, tendencia, _, _ = _analisar_tendencia(valores_num)

        self._responder({
            "sucesso": True,
            "sensor_id": sensor_id,
            "tipo_dado": tipo_dado,
            "anomalias": anomalias,
            "total_anomalias": len(anomalias),
            "tendencia": tendencia,
        })

    def _previsao(self, dados):
        sensor_id = dados.get("sensor_id", "")
        tipo_dado = dados.get("tipo_dado", "")
        valores = dados.get("valores", [])

        if not valores:
            self._responder({"sucesso": False, "erro": "Lista de valores vazia"})
            return

        valores_num = []
        for v in valores:
            try:
                valores_num.append(float(v))
            except (ValueError, TypeError):
                continue

        resultado = prever_proximo(valores_num)
        if resultado is None:
            self._responder({"sucesso": False, "erro": "Erro ao calcular previsao"})
            return

        resultado["sensor_id"] = sensor_id
        resultado["tipo_dado"] = tipo_dado
        resultado["sucesso"] = True
        self._responder(resultado)

    def log_message(self, format, *args):
        print(f"[Analise] {args[0]} {args[1]} {args[2]}")


def _analisar_tendencia(valores):
    n = len(valores)
    if n < 2:
        return 0, "estavel", "baixo", 0

    x = list(range(n))
    y = valores
    sum_x = sum(x)
    sum_y = sum(y)
    sum_xy = sum(x[i] * y[i] for i in range(n))
    sum_xx = sum(xi * xi for xi in x)
    denominador = n * sum_xx - sum_x * sum_x
    inclinacao = (n * sum_xy - sum_x * sum_y) / denominador if denominador != 0 else 0

    tendencia = "subindo" if inclinacao > 0.01 else ("descendo" if inclinacao < -0.01 else "estavel")
    risco = "alto" if abs(inclinacao) > 1.0 else ("medio" if abs(inclinacao) > 0.3 else "baixo")
    media = sum(valores) / n
    return inclinacao, tendencia, risco, media


if __name__ == "__main__":
    server = HTTPServer(("0.0.0.0", PORT), RPCHandler)
    print(f"┌──────────────────────────────────────────────────────────────┐")
    print(f"│          SERVICO DE ANALISE E PREVISAO (RPC)                │")
    print(f"├──────────────────────────────────────────────────────────────┤")
    print(f"│  Porto: {PORT}")
    print(f"│  Metodos: /rpc/estatisticas, /rpc/padroes, /rpc/previsao    │")
    print(f"│  Tecnicas: Media movel, Regressao linear, Z-Score           │")
    print(f"└──────────────────────────────────────────────────────────────┘")
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nServico de Analise terminado.")
        server.server_close()
