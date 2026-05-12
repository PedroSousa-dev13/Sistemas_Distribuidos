using System.Net;
using System.Text;
using System.Text.Json;
using Moq;
using Moq.Protected;
using Servidor;

namespace Servidor.Tests;

public class AnaliseClientTests
{
    private static Mock<HttpMessageHandler> CreateMockHandler(string responseJson, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json"),
            });
        return mock;
    }

    [Fact]
    public async Task CalcularEstatisticasAsync_DeveRetornarResultado()
    {
        var expected = new EstatisticasResult
        {
            Sucesso = true,
            SensorId = "sensor-01",
            TipoDado = "temperatura",
            Count = 5,
            Media = 3.0,
            Mediana = 3.0,
            DesvioPadrao = 1.4142,
            Minimo = 1.0,
            Maximo = 5.0,
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new AnaliseClient(new HttpClient(mock.Object));

        var result = await client.CalcularEstatisticasAsync("sensor-01", "temperatura", new List<double> { 1, 2, 3, 4, 5 });

        Assert.NotNull(result);
        Assert.True(result.Sucesso);
        Assert.Equal(5, result.Count);
        Assert.Equal(3.0, result.Media);
        Assert.Equal(1.0, result.Minimo);
        Assert.Equal(5.0, result.Maximo);
    }

    [Fact]
    public async Task CalcularEstatisticasAsync_MediaDeValoresIguais()
    {
        var expected = new EstatisticasResult
        {
            Sucesso = true,
            Count = 4,
            Media = 7.0,
            DesvioPadrao = 0.0,
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new AnaliseClient(new HttpClient(mock.Object));

        var result = await client.CalcularEstatisticasAsync("sensor-01", "temperatura", new List<double> { 7, 7, 7, 7 });

        Assert.NotNull(result);
        Assert.Equal(7.0, result.Media);
        Assert.Equal(0.0, result.DesvioPadrao);
    }

    [Fact]
    public async Task DetetarPadroesAsync_DeveDetetarAnomalias()
    {
        var expected = new PadroesResult
        {
            Sucesso = true,
            SensorId = "sensor-01",
            TipoDado = "temperatura",
            TotalAnomalias = 1,
            Anomalias = new List<Anomalia>
            {
                new() { Indice = 4, Valor = 100.0, ZScore = 2.64, Descricao = "Anomalia detectada" }
            },
            Tendencia = "subindo",
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new AnaliseClient(new HttpClient(mock.Object));

        var result = await client.DetetarPadroesAsync("sensor-01", "temperatura",
            new List<double> { 10, 11, 10, 12, 100, 11 });

        Assert.NotNull(result);
        Assert.True(result.Sucesso);
        Assert.Equal(1, result.TotalAnomalias);
        Assert.Single(result.Anomalias);
        Assert.Equal("subindo", result.Tendencia);
    }

    [Fact]
    public async Task DetetarPadroesAsync_SemAnomalias()
    {
        var expected = new PadroesResult
        {
            Sucesso = true,
            TotalAnomalias = 0,
            Anomalias = new List<Anomalia>(),
            Tendencia = "estavel",
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new AnaliseClient(new HttpClient(mock.Object));

        var result = await client.DetetarPadroesAsync("sensor-01", "temperatura",
            new List<double> { 10, 11, 10, 12, 11 });

        Assert.NotNull(result);
        Assert.Equal(0, result.TotalAnomalias);
        Assert.Empty(result.Anomalias);
    }

    [Fact]
    public async Task PreverRiscosAsync_DeveRetornarPrevisao()
    {
        var expected = new PrevisaoResult
        {
            Sucesso = true,
            SensorId = "sensor-01",
            TipoDado = "temperatura",
            ProximoValor = 6.0,
            Previsoes = new List<double> { 6.0, 7.0, 8.0 },
            Tendencia = "subindo",
            Risco = "baixo",
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new AnaliseClient(new HttpClient(mock.Object));

        var result = await client.PreverRiscosAsync("sensor-01", "temperatura",
            new List<double> { 1, 2, 3, 4, 5 });

        Assert.NotNull(result);
        Assert.True(result.Sucesso);
        Assert.Equal(6.0, result.ProximoValor);
        Assert.Equal(3, result.Previsoes.Count);
        Assert.Equal("subindo", result.Tendencia);
        Assert.Equal("baixo", result.Risco);
    }

    [Fact]
    public async Task PreverRiscosAsync_TendenciaDescida()
    {
        var expected = new PrevisaoResult
        {
            Sucesso = true,
            ProximoValor = 4.0,
            Tendencia = "descendo",
            Risco = "medio",
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new AnaliseClient(new HttpClient(mock.Object));

        var result = await client.PreverRiscosAsync("sensor-01", "temperatura",
            new List<double> { 10, 9, 8, 7, 6 });

        Assert.NotNull(result);
        Assert.Equal("descendo", result.Tendencia);
    }

    [Fact]
    public async Task PreverRiscosAsync_ErroHttp_DeveRetornarNull()
    {
        var mock = CreateMockHandler("", HttpStatusCode.InternalServerError);
        var client = new AnaliseClient(new HttpClient(mock.Object));

        var result = await client.PreverRiscosAsync("sensor-01", "temperatura",
            new List<double> { 1, 2, 3 });

        Assert.Null(result);
    }

    [Fact]
    public async Task CalcularEstatisticasAsync_ErroHttp_DeveRetornarNull()
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var client = new AnaliseClient(new HttpClient(mock.Object));

        var result = await client.CalcularEstatisticasAsync("sensor-01", "temperatura",
            new List<double> { 1, 2, 3 });

        Assert.Null(result);
    }
}
