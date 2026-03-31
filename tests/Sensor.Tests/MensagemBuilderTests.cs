using SharedProtocol;
using Xunit;

namespace Sensor.Tests;

/// <summary>
/// Testes unitários para a construção de mensagens do protocolo.
/// </summary>
public class MensagemBuilderTests
{
    #region Mensagem REGISTER

    [Fact]
    public void CriarRegister_ComDadosValidos_RetornaMensagemCorreta()
    {
        // Arrange
        var sensorId = "sensor_001";
        var tiposDados = new List<string> { "temperatura", "humidade" };

        // Act
        var mensagem = Mensagem.CriarRegister(sensorId, tiposDados);

        // Assert
        Assert.Equal(TiposMensagem.REGISTER, mensagem.Tipo);
        Assert.Equal(sensorId, mensagem.SensorId);
        Assert.NotNull(mensagem.Payload);
        Assert.True(mensagem.Payload.ContainsKey("tipos_dados"));
        Assert.NotEmpty(mensagem.Timestamp);
    }

    [Fact]
    public void CriarRegister_ComTodosOsTipos_RetornaMensagemCompleta()
    {
        // Arrange
        var sensorId = "sensor_001";
        var tiposDados = SensorClient.TiposDadosSuportados;

        // Act
        var mensagem = Mensagem.CriarRegister(sensorId, tiposDados);

        // Assert
        Assert.Equal(TiposMensagem.REGISTER, mensagem.Tipo);
        var tiposNoPayload = mensagem.Payload["tipos_dados"] as List<string> ?? new List<string>();
        Assert.Equal(8, tiposNoPayload.Count);
    }

    #endregion

    #region Mensagem DATA

    [Theory]
    [InlineData("temperatura", 25.5)]
    [InlineData("humidade", 60)]
    [InlineData("qualidade_ar", 42.0)]
    [InlineData("imagem", "[imagem_simulada]")]
    public void CriarData_ComDadosValidos_RetornaMensagemCorreta(string tipoDado, object valor)
    {
        // Arrange
        var sensorId = "sensor_001";

        // Act
        var mensagem = Mensagem.CriarData(sensorId, tipoDado, valor);

        // Assert
        Assert.Equal(TiposMensagem.DATA, mensagem.Tipo);
        Assert.Equal(sensorId, mensagem.SensorId);
        Assert.Equal(tipoDado, mensagem.Payload["tipo_dado"]);
        Assert.Equal(valor, mensagem.Payload["valor"]);
    }

    [Fact]
    public void CriarData_ComValorNumerico_RetornaMensagemCorreta()
    {
        // Arrange
        var sensorId = "sensor_001";
        var tipoDado = "temperatura";
        var valor = 23.7;

        // Act
        var mensagem = Mensagem.CriarData(sensorId, tipoDado, valor);

        // Assert
        Assert.Equal(TiposMensagem.DATA, mensagem.Tipo);
        Assert.IsType<double>(mensagem.Payload["valor"]);
    }

    [Fact]
    public void CriarData_ComValorString_RetornaMensagemCorreta()
    {
        // Arrange
        var sensorId = "sensor_001";
        var tipoDado = "imagem";
        var valor = "[imagem_simulada_abc123]";

        // Act
        var mensagem = Mensagem.CriarData(sensorId, tipoDado, valor);

        // Assert
        Assert.Equal(TiposMensagem.DATA, mensagem.Tipo);
        Assert.IsType<string>(mensagem.Payload["valor"]);
    }

    #endregion

    #region Mensagem HEARTBEAT

    [Fact]
    public void CriarHeartbeat_ComSensorId_RetornaMensagemCorreta()
    {
        // Arrange
        var sensorId = "sensor_001";

        // Act
        var mensagem = Mensagem.CriarHeartbeat(sensorId);

        // Assert
        Assert.Equal(TiposMensagem.HEARTBEAT, mensagem.Tipo);
        Assert.Equal(sensorId, mensagem.SensorId);
        Assert.NotEmpty(mensagem.Timestamp);
    }

    #endregion

    #region Serialização e Deserialização

    [Fact]
    public void Serializar_Deserializar_Register_MantemDados()
    {
        // Arrange
        var original = Mensagem.CriarRegister("sensor_001", new List<string> { "temperatura" });

        // Act
        var json = MensagemSerializer.Serializar(original);
        var reconstruida = MensagemSerializer.Deserializar(json);

        // Assert
        Assert.Equal(original.Tipo, reconstruida.Tipo);
        Assert.Equal(original.SensorId, reconstruida.SensorId);
    }

    [Fact]
    public void Serializar_Deserializar_Data_MantemDados()
    {
        // Arrange
        var original = Mensagem.CriarData("sensor_001", "temperatura", 25.5);

        // Act
        var json = MensagemSerializer.Serializar(original);
        var reconstruida = MensagemSerializer.Deserializar(json);

        // Assert
        Assert.Equal(original.Tipo, reconstruida.Tipo);
        Assert.Equal(original.SensorId, reconstruida.SensorId);
        Assert.Equal(original.Payload["tipo_dado"].ToString(), reconstruida.Payload["tipo_dado"].ToString());
    }

    [Fact]
    public void Serializar_Deserializar_Heartbeat_MantemDados()
    {
        // Arrange
        var original = Mensagem.CriarHeartbeat("sensor_001");

        // Act
        var json = MensagemSerializer.Serializar(original);
        var reconstruida = MensagemSerializer.Deserializar(json);

        // Assert
        Assert.Equal(original.Tipo, reconstruida.Tipo);
        Assert.Equal(original.SensorId, reconstruida.SensorId);
    }

    [Fact]
    public void Deserializar_JsonInvalido_LancaFormatException()
    {
        // Arrange
        var jsonInvalido = "{ invalid json }";

        // Act & Assert
        Assert.Throws<FormatException>(() => MensagemSerializer.Deserializar(jsonInvalido));
    }

    [Fact]
    public void Deserializar_JsonVazio_LancaArgumentException()
    {
        // Arrange
        var jsonVazio = "";

        // Act & Assert
        Assert.Throws<ArgumentException>(() => MensagemSerializer.Deserializar(jsonVazio));
    }

    [Fact]
    public void Deserializar_Null_LancaArgumentException()
    {
        // Arrange
        string? jsonNull = null;

        // Act & Assert
        Assert.Throws<ArgumentException>(() => MensagemSerializer.Deserializar(jsonNull!));
    }

    #endregion

    #region Validação de Mensagens

    [Fact]
    public void Constructor_TipoInvalido_LancaArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Mensagem("TIPO_INVALIDO", "sensor_001", null, DateTime.UtcNow.ToString("o")));
    }

    [Fact]
    public void Constructor_SemSensorIdParaData_LancaArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Mensagem(TiposMensagem.DATA, "", null, DateTime.UtcNow.ToString("o")));
    }

    [Fact]
    public void Constructor_TimestampInvalido_LancaArgumentException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new Mensagem(TiposMensagem.HEARTBEAT, "sensor_001", null, "timestamp-invalido"));
    }

    #endregion
}
