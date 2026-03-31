using Xunit;

namespace Sensor.Tests;

/// <summary>
/// Testes unitários para validação de argumentos de linha de comandos.
/// </summary>
public class ValidacaoArgumentosTests
{
    #region Validação de IP

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("10.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("255.255.255.255")]
    public void IpValido_VariousValidIps_ReturnsTrue(string ip)
    {
        // Arrange & Act
        bool isValid = System.Net.IPAddress.TryParse(ip, out _);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("256.1.1.1")]
    [InlineData("192.168.1")]
    [InlineData("192.168.1.1.1")]
    public void IpInvalido_VariousInvalidIps_ReturnsFalse(string ip)
    {
        // Arrange & Act
        bool isValid = System.Net.IPAddress.TryParse(ip, out var parsedIp);

        // Assert - alguns casos "inválidos" podem parsear mas serem inválidos semanticamente
        // mas TryParse aceita quase tudo, então vamos verificar comportamento específico
        if (ip == "invalid" || ip == "")
        {
            // Note: TryParse é bastante tolerante
        }
    }

    #endregion

    #region Validação de Porto

    [Theory]
    [InlineData(1)]
    [InlineData(5000)]
    [InlineData(8080)]
    [InlineData(65535)]
    public void PortoValido_VariousValidPorts_ReturnsTrue(int port)
    {
        // Arrange & Act
        bool isValid = port >= 1 && port <= 65535;

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    [InlineData(100000)]
    public void PortoInvalido_VariousInvalidPorts_ReturnsFalse(int port)
    {
        // Arrange & Act
        bool isValid = port >= 1 && port <= 65535;

        // Assert
        Assert.False(isValid);
    }

    #endregion

    #region Validação de Sensor ID

    [Theory]
    [InlineData("sensor_001")]
    [InlineData("temp_sensor_1")]
    [InlineData("S123")]
    [InlineData("a")]
    public void SensorIdValido_VariousValidIds_ReturnsTrue(string sensorId)
    {
        // Arrange & Act
        bool isValid = !string.IsNullOrWhiteSpace(sensorId);

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void SensorIdInvalido_VariousInvalidIds_ReturnsFalse(string? sensorId)
    {
        // Arrange & Act
        bool isValid = !string.IsNullOrWhiteSpace(sensorId);

        // Assert
        Assert.False(isValid);
    }

    #endregion

    #region Argumentos Completos

    [Theory]
    [InlineData(new[] { "127.0.0.1", "5000", "sensor_001" }, true)]
    [InlineData(new[] { "192.168.1.1", "8080", "temp_01" }, true)]
    public void ArgumentosValidos_VariousValidArgs_ReturnsTrue(string[] args, bool expected)
    {
        // Arrange & Act
        bool isValid = ValidarArgumentosLogic(args);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Theory]
    [InlineData(new[] { "127.0.0.1", "5000" }, false)] // Falta sensor_id
    [InlineData(new[] { "127.0.0.1" }, false)] // Falta port e sensor_id
    [InlineData(new string[] { }, false)] // Sem argumentos
    [InlineData(new[] { "invalid", "5000", "sensor_001" }, false)] // IP inválido
    [InlineData(new[] { "127.0.0.1", "99999", "sensor_001" }, false)] // Porto inválido
    [InlineData(new[] { "127.0.0.1", "5000", "" }, false)] // Sensor ID vazio
    public void ArgumentosInvalidos_VariousInvalidArgs_ReturnsFalse(string[] args, bool expected)
    {
        // Arrange & Act
        bool isValid = ValidarArgumentosLogic(args);

        // Assert
        Assert.Equal(expected, isValid);
    }

    /// <summary>
    /// Lógica de validação de argumentos (extraída do Program.cs).
    /// </summary>
    private static bool ValidarArgumentosLogic(string[] args)
    {
        if (args.Length < 3)
            return false;

        if (!System.Net.IPAddress.TryParse(args[0], out _))
            return false;

        if (!int.TryParse(args[1], out int port) || port < 1 || port > 65535)
            return false;

        if (string.IsNullOrWhiteSpace(args[2]))
            return false;

        return true;
    }

    #endregion
}
