import sys
sys.path.insert(0, "src/Analise")
from detecao_padroes import analisar_padroes


class TestAnalisarPadroes:
    def test_lista_vazia(self):
        r = analisar_padroes([])
        assert r["total_anomalias"] == 0
        assert r["anomalias"] == []
        assert r["taxa_anomalia"] == 0.0
        assert r["estatisticas"] is None

    def test_sem_anomalias(self):
        r = analisar_padroes([10, 11, 10, 12, 11, 10])
        assert r["total_anomalias"] == 0
        assert r["anomalias"] == []
        assert r["taxa_anomalia"] == 0.0
        assert r["estatisticas"] is not None

    def test_com_anomalias(self):
        r = analisar_padroes([10, 10, 10, 100, 10, 10])
        assert r["total_anomalias"] >= 1
        assert r["taxa_anomalia"] > 0
        assert r["estatisticas"] is not None

    def test_limiar_personalizado(self):
        r = analisar_padroes([10, 10, 15, 10, 10], limiar=5.0)
        assert r["total_anomalias"] == 0

    def test_estrutura_resultado(self):
        r = analisar_padroes([1, 2, 3, 4, 5])
        assert "estatisticas" in r
        assert "anomalias" in r
        assert "total_anomalias" in r
        assert "taxa_anomalia" in r
        assert isinstance(r["anomalias"], list)
        assert isinstance(r["total_anomalias"], int)
        assert isinstance(r["taxa_anomalia"], float)
