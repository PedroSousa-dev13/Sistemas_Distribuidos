using System.Net;
using System.Text;
using System.Text.Json;
using Gateway;
using Moq;
using Moq.Protected;

namespace Gateway.Tests;

public class PreProcessamentoClientTests
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
    public async Task UniformizarDadosAsync_DeveRetornarResultadoComSucesso()
    {
        var expected = new PreProcessamentoRpcResult
        {
            Sucesso = true,
            SensorId = "sensor-01",
            TipoDado = "temperatura",
            ValorUniformizado = 100.0,
            Unidade = "celsius",
            Timestamp = "2026-01-01T00:00:00Z",
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new PreProcessamentoClient(new HttpClient(mock.Object));

        var result = await client.UniformizarDadosAsync("sensor-01", "temperatura", 212, "2026-01-01T00:00:00Z", "FAHRENHEIT");

        Assert.NotNull(result);
        Assert.True(result.Sucesso);
        Assert.Equal(100.0, result.ValorUniformizado);
        Assert.Equal("celsius", result.Unidade);
    }

    [Fact]
    public async Task UniformizarDadosAsync_DeveConverterFahrenheitParaCelsius()
    {
        var expected = new PreProcessamentoRpcResult
        {
            Sucesso = true,
            SensorId = "sensor-01",
            TipoDado = "temperatura",
            ValorUniformizado = 0.0,
            Unidade = "celsius",
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new PreProcessamentoClient(new HttpClient(mock.Object));

        var result = await client.UniformizarDadosAsync("sensor-01", "temperatura", 32, "2026-01-01T00:00:00Z", "FAHRENHEIT");

        Assert.NotNull(result);
        Assert.Equal(0.0, result.ValorUniformizado);
    }

    [Fact]
    public async Task UniformizarDadosAsync_ErroHttp_DeveRetornarResultadoComErro()
    {
        var mock = CreateMockHandler("", HttpStatusCode.InternalServerError);
        var client = new PreProcessamentoClient(new HttpClient(mock.Object));

        var result = await client.UniformizarDadosAsync("sensor-01", "temperatura", 25, "2026-01-01T00:00:00Z");

        Assert.Null(result);
    }

    [Fact]
    public async Task ValidarDadosAsync_DeveRetornarValido()
    {
        var expected = new ValidacaoRpcResult
        {
            Valido = true,
            SensorId = "sensor-01",
            TipoDado = "temperatura",
            Erros = new List<string>(),
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new PreProcessamentoClient(new HttpClient(mock.Object));

        var result = await client.ValidarDadosAsync("sensor-01", "temperatura", 25.0);

        Assert.NotNull(result);
        Assert.True(result.Valido);
        Assert.Empty(result.Erros);
    }

    [Fact]
    public async Task ValidarDadosAsync_DeveRetornarInvalido()
    {
        var expected = new ValidacaoRpcResult
        {
            Valido = false,
            SensorId = "sensor-01",
            TipoDado = "temperatura",
            Erros = new List<string> { "Valor acima do maximo" },
        };

        var mock = CreateMockHandler(JsonSerializer.Serialize(expected));
        var client = new PreProcessamentoClient(new HttpClient(mock.Object));

        var result = await client.ValidarDadosAsync("sensor-01", "temperatura", 150.0);

        Assert.NotNull(result);
        Assert.False(result.Valido);
        Assert.NotEmpty(result.Erros);
    }

    [Fact]
    public async Task ValidarDadosAsync_Timeout_DeveRetornarNull()
    {
        var mock = new Mock<HttpMessageHandler>(MockBehavior.Strict);
        mock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException("Timeout"));

        var client = new PreProcessamentoClient(new HttpClient(mock.Object));

        var result = await client.ValidarDadosAsync("sensor-01", "temperatura", 25.0);

        Assert.Null(result);
    }
}
