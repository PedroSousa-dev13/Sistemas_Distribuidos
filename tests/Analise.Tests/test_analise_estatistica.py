import sys, math
sys.path.insert(0, "src/Analise")
from analise_estatistica import calcular_estatisticas, detetar_anomalias, prever_proximo


class TestCalcularEstatisticas:
    def test_lista_vazia(self):
        assert calcular_estatisticas([]) is None

    def test_um_elemento(self):
        r = calcular_estatisticas([5])
        assert r["count"] == 1
        assert r["media"] == 5.0
        assert r["mediana"] == 5.0
        assert r["minimo"] == r["maximo"] == 5.0
        assert r["desvio_padrao"] == 0.0
        assert r["variancia"] == 0.0

    def test_dois_elementos(self):
        r = calcular_estatisticas([1, 3])
        assert r["count"] == 2
        assert r["media"] == 2.0
        assert r["mediana"] == 2.0
        assert r["minimo"] == 1.0
        assert r["maximo"] == 3.0

    def test_n_impar(self):
        r = calcular_estatisticas([1, 2, 3])
        assert r["count"] == 3
        assert r["media"] == 2.0
        assert r["mediana"] == 2.0

    def test_n_par(self):
        r = calcular_estatisticas([1, 2, 3, 4])
        assert r["count"] == 4
        assert r["media"] == 2.5
        assert r["mediana"] == 2.5

    def test_quartis(self):
        r = calcular_estatisticas([1, 2, 3, 4, 5])
        assert r["q1"] == 2.0
        assert r["q3"] == 4.0

    def test_valores_negativos(self):
        r = calcular_estatisticas([-5, 0, 5])
        assert r["media"] == 0.0
        assert r["minimo"] == -5.0
        assert r["maximo"] == 5.0

    def test_precisao(self):
        r = calcular_estatisticas([1.23456, 2.34567, 3.45678])
        assert r["media"] < 2.346
        assert all(isinstance(v, float) for v in [r["media"], r["mediana"], r["desvio_padrao"]])


class TestDetetarAnomalias:
    def test_menos_de_3(self):
        assert detetar_anomalias([1, 2]) == []
        assert detetar_anomalias([]) == []
        assert detetar_anomalias([42]) == []

    def test_variancia_zero(self):
        assert detetar_anomalias([5, 5, 5, 5]) == []

    def test_sem_anomalias(self):
        r = detetar_anomalias([10, 11, 10, 12, 11, 10, 11, 12])
        assert r == []

    def test_com_anomalia(self):
        r = detetar_anomalias([10, 10, 10, 10, 100, 10, 10, 10])
        assert len(r) >= 1
        assert r[0]["indice"] == 4
        assert r[0]["valor"] == 100.0

    def test_limiar_personalizado(self):
        r_baixo = detetar_anomalias([10, 10, 15, 10, 10], limiar=0.5)
        assert len(r_baixo) >= 1
        r_alto = detetar_anomalias([10, 10, 15, 10, 10], limiar=5.0)
        assert r_alto == []

    def test_nivel_critico(self):
        r = detetar_anomalias([0]*17 + [100] + [0]*17)
        assert any("critico" in a["descricao"] for a in r)


class TestPreverProximo:
    def test_lista_vazia(self):
        assert prever_proximo([]) is None

    def test_um_elemento(self):
        r = prever_proximo([42])
        assert r["proximo_valor"] == 42.0
        assert r["tendencia"] == "estavel"
        assert r["risco"] == "baixo"

    def test_dois_elementos(self):
        r = prever_proximo([10, 20])
        assert r["proximo_valor"] > 20
        assert r["tendencia"] == "subindo"

    def test_previsoes_tamanho(self):
        r = prever_proximo([1, 2, 3, 4, 5])
        assert len(r["previsoes"]) == 3

    def test_tendencia_descendo(self):
        r = prever_proximo([100, 90, 80, 70])
        assert r["tendencia"] == "descendo"

    def test_media_movel_com_janela(self):
        r = prever_proximo([1, 2, 3, 4, 5, 6, 7, 8, 9, 10])
        expected_mm = sum([6, 7, 8, 9, 10]) / 5
        assert r["media_movel"] == expected_mm
