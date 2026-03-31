using SharedProtocol;
using Xunit;

namespace Sensor.Tests;

/// <summary>
/// Testes unitários para a classe SensorClient.
/// </summary>
public class SensorClientTests
{
    #region Construtor e Validação

    [Fact]
    public void Constructor_ValidosArgs_CriaInstancia()
    {
        // Arrange & Act
        var sensor = new SensorClient("127.0.0.1", 5000, "sensor_001");

        // Assert
        Assert.Equal("127.0.0.1", sensor.GatewayIp);
        Assert.Equal(5000, sensor.GatewayPort);
        Assert.Equal("sensor_001", sensor.SensorId);
    }

    [Theory]
    [InlineData("", 5000, "sensor_001")]
    [InlineData("   ", 5000, "sensor_001")]
    public void Constructor_IpInvalido_LancaArgumentException(string? ip, int port, string sensorId)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new SensorClient(ip!, port, sensorId));
    }

    [Fact]
    public void Constructor_IpNull_LancaArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorClient(null!, 5000, "sensor_001"));
    }

    [Theory]
    [InlineData("127.0.0.1", 5000, "")]
    [InlineData("127.0.0.1", 5000, "   ")]
    public void Constructor_SensorIdInvalido_LancaArgumentException(string ip, int port, string? sensorId)
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => new SensorClient(ip, port, sensorId!));
    }

    [Fact]
    public void Constructor_SensorIdNull_LancaArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SensorClient("127.0.0.1", 5000, null!));
    }

    #endregion

    #region Tipos de Dados Suportados

    [Fact]
    public void TiposDadosSuportados_ContemTodosOsTipos()
    {
        // Arrange
        var tiposEsperados = new[]
        {
            "temperatura", "humidade", "qualidade_ar", "ruido",
            "pm25", "pm10", "luminosidade", "imagem"
        };

        // Act
        var tipos = SensorClient.TiposDadosSuportados;

        // Assert
        Assert.Equal(8, tipos.Count);
        foreach (var tipo in tiposEsperados)
        {
            Assert.Contains(tipo, tipos);
        }
    }

    #endregion

    #region Estado Inicial

    [Fact]
    public void EstaConectado_Inicialmente_RetornaFalse()
    {
        // Arrange
        var sensor = new SensorClient("127.0.0.1", 5000, "sensor_001");

        // Act
        var conectado = sensor.EstaConectado();

        // Assert
        Assert.False(conectado);
    }

    #endregion

    #region Eventos de Log

    [Fact]
    public void OnLog_QuandoEventoSubscrito_RecebeMensagens()
    {
        // Arrange
        var sensor = new SensorClient("127.0.0.1", 5000, "sensor_001");
        var mensagensRecebidas = new List<string>();
        sensor.OnLog += (s, msg) => mensagensRecebidas.Add(msg);

        // Act - Dispose gera log "Sensor terminado."
        sensor.Dispose();

        // Assert - verificar que a mensagem de log foi recebida
        Assert.Contains(mensagensRecebidas, msg => msg.Contains("Sensor terminado"));
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_PodeSerChamadoMultiplasVezes()
    {
        // Arrange
        var sensor = new SensorClient("127.0.0.1", 5000, "sensor_001");

        // Act & Assert - não deve lançar exceção
        sensor.Dispose();
        sensor.Dispose(); // Chamada idempotente
    }

    #endregion
}
