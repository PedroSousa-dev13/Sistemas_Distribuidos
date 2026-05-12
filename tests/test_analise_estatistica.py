import sys
import math
sys.path.insert(0, r"src\Analise")
from analise_estatistica import calcular_estatisticas, detetar_anomalias, prever_proximo


def test_calcular_estatisticas_lista_vazia():
    assert calcular_estatisticas([]) is None


def test_calcular_estatisticas_valor_unico():
    r = calcular_estatisticas([5])
    assert r["count"] == 1
    assert r["media"] == 5
    assert r["mediana"] == 5
    assert r["desvio_padrao"] == 0
    assert r["minimo"] == 5
    assert r["maximo"] == 5


def test_calcular_estatisticas_normais():
    r = calcular_estatisticas([1, 2, 3, 4, 5])
    assert r["count"] == 5
    assert r["media"] == 3.0
    assert r["mediana"] == 3
    assert r["minimo"] == 1
    assert r["maximo"] == 5
    assert r["q1"] == 2
    assert r["q3"] == 4


def test_calcular_estatisticas_pares():
    r = calcular_estatisticas([2, 4, 6, 8])
    assert r["count"] == 4
    assert r["media"] == 5.0
    assert r["mediana"] == 5.0
    assert r["minimo"] == 2
    assert r["maximo"] == 8


def test_calcular_estatisticas_valores_iguais():
    r = calcular_estatisticas([7, 7, 7, 7])
    assert r["media"] == 7
    assert r["desvio_padrao"] == 0
    assert r["variancia"] == 0


def test_calcular_estatisticas_decimais():
    r = calcular_estatisticas([1.5, 2.5, 3.5])
    assert r["media"] == 2.5
    assert r["mediana"] == 2.5
    assert r["minimo"] == 1.5
    assert r["maximo"] == 3.5


def test_detetar_anomalias_sem_anomalias():
    anomalias = detetar_anomalias([10, 11, 10, 12, 11, 10, 11])
    assert len(anomalias) == 0


def test_detetar_anomalias_com_anomalia():
    anomalias = detetar_anomalias([10, 11, 10, 12, 11, 10, 100, 11])
    assert len(anomalias) >= 1
    assert anomalias[0]["valor"] == 100.0


def test_detetar_anomalias_poucos_dados():
    anomalias = detetar_anomalias([1, 2])
    assert len(anomalias) == 0


def test_detetar_anomalias_todos_iguais():
    anomalias = detetar_anomalias([5, 5, 5, 5, 5])
    assert len(anomalias) == 0


def test_prever_proximo_lista_vazia():
    assert prever_proximo([]) is None


def test_prever_proximo_valor_unico():
    r = prever_proximo([10])
    assert r["proximo_valor"] == 10.0
    assert r["tendencia"] == "estavel"
    assert r["risco"] == "baixo"


def test_prever_proximo_tendencia_subida():
    r = prever_proximo([1, 2, 3, 4, 5])
    assert r["proximo_valor"] > 5
    assert r["tendencia"] == "subindo"


def test_prever_proximo_tendencia_descida():
    r = prever_proximo([10, 9, 8, 7, 6])
    assert r["proximo_valor"] < 6
    assert r["tendencia"] == "descendo"


def test_prever_proximo_constante():
    r = prever_proximo([5, 5, 5, 5, 5])
    assert r["proximo_valor"] == 5.0
    assert r["tendencia"] == "estavel"
    assert r["risco"] == "baixo"


def test_prever_proximo_3_previsoes():
    r = prever_proximo([1, 2, 3, 4, 5])
    assert len(r["previsoes"]) == 3


def test_prever_proximo_media_movel():
    r = prever_proximo([10, 20, 30])
    assert r["media_movel"] == 20.0
