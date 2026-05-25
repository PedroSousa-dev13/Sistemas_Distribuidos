import math

def calcular_estatisticas(valores):
    n = len(valores)
    if n == 0:
        return None

    sorted_vals = sorted(valores)
    media = sum(valores) / n
    variancia = sum((x - media) ** 2 for x in valores) / n
    desvio = math.sqrt(variancia)

    if n % 2 == 1:
        mediana = sorted_vals[n // 2]
    else:
        mediana = (sorted_vals[n // 2 - 1] + sorted_vals[n // 2]) / 2

    def quartil(sorted_list, q):
        k = (len(sorted_list) - 1) * q
        f = math.floor(k)
        c = math.ceil(k)
        if f == c:
            return sorted_list[int(k)]
        return sorted_list[f] * (c - k) + sorted_list[c] * (k - f)

    q1 = quartil(sorted_vals, 0.25)
    q3 = quartil(sorted_vals, 0.75)

    return {
        "count": n,
        "media": round(media, 4),
        "mediana": round(mediana, 4),
        "desvio_padrao": round(desvio, 4),
        "variancia": round(variancia, 4),
        "minimo": round(min(valores), 4),
        "maximo": round(max(valores), 4),
        "q1": round(q1, 4),
        "q3": round(q3, 4),
    }


def detetar_anomalias(valores, limiar=2.0):
    n = len(valores)
    if n < 3:
        return []

    media = sum(valores) / n
    variancia = sum((x - media) ** 2 for x in valores) / n
    if variancia == 0:
        return []

    desvio = math.sqrt(variancia)

    anomalias = []
    for i, v in enumerate(valores):
        z_score = (v - media) / desvio
        if abs(z_score) > limiar:
            nivel = "critico" if abs(z_score) > 3.0 else "moderado"
            anomalias.append({
                "indice": i,
                "valor": round(v, 4),
                "z_score": round(z_score, 4),
                "descricao": f"Valor {v:.2f} ({nivel}, z={z_score:.2f})",
            })

    return anomalias


def prever_proximo(valores, janela=5):
    n = len(valores)
    if n == 0:
        return None

    if n < janela:
        janela = n

    media_movel = sum(valores[-janela:]) / janela

    if n >= 2:
        x = list(range(n))
        y = valores
        n_pts = n
        sum_x = sum(x)
        sum_y = sum(y)
        sum_xy = sum(x[i] * y[i] for i in range(n_pts))
        sum_xx = sum(xi * xi for xi in x)

        denominador = n_pts * sum_xx - sum_x * sum_x
        if denominador != 0:
            inclinacao = (n_pts * sum_xy - sum_x * sum_y) / denominador
        else:
            inclinacao = 0.0

        intercept = (sum_y - inclinacao * sum_x) / n_pts
        tendencia = "subindo" if inclinacao > 0.01 else ("descendo" if inclinacao < -0.01 else "estavel")
        risco = "alto" if abs(inclinacao) > 1.0 else ("medio" if abs(inclinacao) > 0.3 else "baixo")
        proximo = round(intercept + inclinacao * n, 4)
    else:
        tendencia = "estavel"
        risco = "baixo"
        inclinacao = 0.0
        intercept = 0.0
        proximo = round(media_movel, 4)

    if n >= 2:
        previsoes = [round(intercept + inclinacao * (n + i), 4) for i in range(1, 4)]
    else:
        previsoes = [round(media_movel, 4) for _ in range(1, 4)]

    return {
        "previsoes": previsoes,
        "proximo_valor": proximo,
        "tendencia": tendencia,
        "risco": risco,
        "media_movel": round(media_movel, 4),
        "inclinacao": round(inclinacao, 4) if n >= 2 else 0.0,
    }
