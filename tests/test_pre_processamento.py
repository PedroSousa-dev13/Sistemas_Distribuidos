import sys
sys.path.insert(0, r"src\PreProcessamento")
from servico import uniformizar_valor, validar_valor, LIMITES


def test_uniformizar_temperatura_celsius_mantem():
    r = uniformizar_valor("temperatura", 25.0, "JSON")
    assert r == 25.0


def test_uniformizar_temperatura_fahrenheit_para_celsius():
    r = uniformizar_valor("temperatura", 212.0, "FAHRENHEIT")
    assert r == 100.0


def test_uniformizar_temperatura_kelvin_para_celsius():
    r = uniformizar_valor("temperatura", 373.15, "KELVIN")
    assert r == 100.0


def test_uniformizar_temperatura_fahrenheit_0():
    r = uniformizar_valor("temperatura", 32.0, "FAHRENHEIT")
    assert r == 0.0


def test_uniformizar_humidade_mantem():
    r = uniformizar_valor("humidade", 45.0, "JSON")
    assert r == 45.0


def test_uniformizar_humidade_fracao_para_percentagem():
    r = uniformizar_valor("humidade", 0.45, "FRACAO")
    assert r == 45.0


def test_uniformizar_humidade_fracao_1():
    r = uniformizar_valor("humidade", 1.0, "FRACAO")
    assert r == 100.0


def test_validar_valor_dentro_limites():
    valido, erros = validar_valor("temperatura", 25.0)
    assert valido is True
    assert len(erros) == 0


def test_validar_valor_acima_maximo():
    valido, erros = validar_valor("temperatura", 150.0)
    assert valido is False
    assert any("acima" in e for e in erros)


def test_validar_valor_abaixo_minimo():
    valido, erros = validar_valor("temperatura", -100.0)
    assert valido is False
    assert any("abaixo" in e for e in erros)


def test_validar_valor_no_limite_maximo():
    valido, erros = validar_valor("temperatura", 100.0)
    assert valido is True


def test_validar_valor_no_limite_minimo():
    valido, erros = validar_valor("temperatura", -50.0)
    assert valido is True


def test_validar_humidade_0():
    valido, erros = validar_valor("humidade", 0.0)
    assert valido is True


def test_validar_humidade_100():
    valido, erros = validar_valor("humidade", 100.0)
    assert valido is True


def test_validar_humidade_acima_100():
    valido, erros = validar_valor("humidade", 150.0)
    assert valido is False


def test_validar_tipo_desconhecido():
    valido, erros = validar_valor("tipo_invalido", 50.0)
    assert valido is True
    assert len(erros) == 0


def test_validar_pm25_dentro():
    valido, _ = validar_valor("pm25", 50.0)
    assert valido is True


def test_validar_pm25_acima():
    valido, _ = validar_valor("pm25", 2000.0)
    assert valido is False


def test_limites_tipos():
    assert "temperatura" in LIMITES
    assert "humidade" in LIMITES
    assert "pm25" in LIMITES
    assert "pm10" in LIMITES
    assert "qualidade_ar" in LIMITES
    assert "ruido" in LIMITES
    assert "luminosidade" in LIMITES
