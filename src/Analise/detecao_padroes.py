from analise_estatistica import detetar_anomalias, calcular_estatisticas

def analisar_padroes(valores, limiar=2.0):
    anomalias = detetar_anomalias(valores, limiar)
    estatisticas = calcular_estatisticas(valores)

    return {
        "estatisticas": estatisticas,
        "anomalias": anomalias,
        "total_anomalias": len(anomalias),
        "taxa_anomalia": round(len(anomalias) / len(valores) * 100, 2) if valores else 0,
    }
