using System;
using System.Collections.Generic;
using Xunit;
using SharedProtocol;

namespace SharedProtocol.Tests;

/// <summary>
/// Testes unitários para validação da classe Mensagem.
/// Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5
/// </summary>
public class MensagemValidacaoTests
{
    private static readonly string ValidTimestamp = DateTime.UtcNow.ToString("o");
    private static readonly string ValidSensorId = "SENSOR_001";
    private static readonly Dictionary<string, object> EmptyPayload = new();

    // ─── Requirement 7.1: tipo não pode ser nulo ou vazio ───────────────────────

    [Fact]
    public void Construtor_TipoNulo_LancaArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(null!, ValidSensorId, EmptyPayload, ValidTimestamp));

        Assert.Contains("Tipo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Construtor_TipoVazio_LancaArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem("", ValidSensorId, EmptyPayload, ValidTimestamp));

        Assert.Contains("Tipo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Construtor_TipoEspacoEmBranco_LancaArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem("   ", ValidSensorId, EmptyPayload, ValidTimestamp));

        Assert.Contains("Tipo", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Requirement 7.2: tipo deve ser um dos tipos válidos ────────────────────

    [Theory]
    [InlineData("INVALIDO")]
    [InlineData("register")]       // case-sensitive
    [InlineData("DATA_EXTRA")]
    [InlineData("UNKNOWN_TYPE")]
    public void Construtor_TipoInvalido_LancaArgumentException(string tipoInvalido)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(tipoInvalido, ValidSensorId, EmptyPayload, ValidTimestamp));

        Assert.Contains(tipoInvalido, ex.Message);
    }

    [Theory]
    [InlineData(TiposMensagem.REGISTER)]
    [InlineData(TiposMensagem.REGISTER_OK)]
    [InlineData(TiposMensagem.REGISTER_ERR)]
    [InlineData(TiposMensagem.DATA)]
    [InlineData(TiposMensagem.DATA_ACK)]
    [InlineData(TiposMensagem.HEARTBEAT)]
    [InlineData(TiposMensagem.HEARTBEAT_ACK)]
    [InlineData(TiposMensagem.ERROR)]
    public void Construtor_TipoValido_NaoLancaExcecao(string tipoValido)
    {
        // Tipos que requerem sensorId precisam de um sensorId válido
        var sensorId = TiposMensagem.RequerSensorId(tipoValido) ? ValidSensorId : "";
        var mensagem = new Mensagem(tipoValido, sensorId, EmptyPayload, ValidTimestamp);
        Assert.Equal(tipoValido, mensagem.Tipo);
    }

    // ─── Requirement 7.3: sensorId obrigatório para DATA, HEARTBEAT, REGISTER ──

    [Theory]
    [InlineData(TiposMensagem.DATA)]
    [InlineData(TiposMensagem.HEARTBEAT)]
    [InlineData(TiposMensagem.REGISTER)]
    public void Construtor_SensorIdVazioParaTipoQueRequer_LancaArgumentException(string tipo)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(tipo, "", EmptyPayload, ValidTimestamp));

        Assert.Contains("SensorId", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(tipo, ex.Message);
    }

    [Theory]
    [InlineData(TiposMensagem.DATA)]
    [InlineData(TiposMensagem.HEARTBEAT)]
    [InlineData(TiposMensagem.REGISTER)]
    public void Construtor_SensorIdNuloParaTipoQueRequer_LancaArgumentException(string tipo)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(tipo, null!, EmptyPayload, ValidTimestamp));

        Assert.Contains("SensorId", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Requirement 7.3: sensorId opcional para REGISTER_OK, REGISTER_ERR, DATA_ACK, HEARTBEAT_ACK, ERROR ──

    [Theory]
    [InlineData(TiposMensagem.REGISTER_OK)]
    [InlineData(TiposMensagem.REGISTER_ERR)]
    [InlineData(TiposMensagem.DATA_ACK)]
    [InlineData(TiposMensagem.HEARTBEAT_ACK)]
    [InlineData(TiposMensagem.ERROR)]
    public void Construtor_SensorIdVazioParaTipoQueNaoRequer_NaoLancaExcecao(string tipo)
    {
        var mensagem = new Mensagem(tipo, "", EmptyPayload, ValidTimestamp);
        Assert.Equal(tipo, mensagem.Tipo);
    }

    [Theory]
    [InlineData(TiposMensagem.REGISTER_OK)]
    [InlineData(TiposMensagem.REGISTER_ERR)]
    [InlineData(TiposMensagem.DATA_ACK)]
    [InlineData(TiposMensagem.HEARTBEAT_ACK)]
    [InlineData(TiposMensagem.ERROR)]
    public void Construtor_SensorIdNuloParaTipoQueNaoRequer_NaoLancaExcecao(string tipo)
    {
        var mensagem = new Mensagem(tipo, null!, EmptyPayload, ValidTimestamp);
        Assert.Equal(tipo, mensagem.Tipo);
        Assert.Equal(string.Empty, mensagem.SensorId);
    }

    // ─── Requirement 7.4: timestamp deve ser ISO 8601 válido ────────────────────

    [Theory]
    [InlineData("nao-e-data")]
    [InlineData("2024/01/15")]
    [InlineData("15-01-2024")]
    [InlineData("")]
    [InlineData("   ")]
    public void Construtor_TimestampInvalido_LancaArgumentException(string timestampInvalido)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(TiposMensagem.DATA_ACK, "", EmptyPayload, timestampInvalido));

        Assert.Contains("Timestamp", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Construtor_TimestampNulo_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            new Mensagem(TiposMensagem.DATA_ACK, "", EmptyPayload, null!));
    }

    [Theory]
    [InlineData("2024-01-15T10:30:00.000Z")]
    [InlineData("2024-01-15T10:30:00+00:00")]
    public void Construtor_TimestampIso8601Valido_NaoLancaExcecao(string timestampValido)
    {
        var mensagem = new Mensagem(TiposMensagem.DATA_ACK, "", EmptyPayload, timestampValido);
        Assert.Equal(timestampValido, mensagem.Timestamp);
    }

    [Fact]
    public void Construtor_TimestampDateTimeUtcNow_NaoLancaExcecao()
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        var mensagem = new Mensagem(TiposMensagem.DATA_ACK, "", EmptyPayload, timestamp);
        Assert.Equal(timestamp, mensagem.Timestamp);
    }

    // ─── Requirement 7.5: mensagens de erro descritivas ─────────────────────────

    [Fact]
    public void Construtor_TipoNulo_MensagemErroContemNomeCampo()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(null!, ValidSensorId, EmptyPayload, ValidTimestamp));

        Assert.Equal("tipo", ex.ParamName);
    }

    [Fact]
    public void Construtor_TipoInvalido_MensagemErroContemTipoFornecido()
    {
        const string tipoInvalido = "TIPO_INEXISTENTE";
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(tipoInvalido, ValidSensorId, EmptyPayload, ValidTimestamp));

        Assert.Contains(tipoInvalido, ex.Message);
        Assert.Equal("tipo", ex.ParamName);
    }

    [Fact]
    public void Construtor_SensorIdVazioParaData_MensagemErroContemTipo()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(TiposMensagem.DATA, "", EmptyPayload, ValidTimestamp));

        Assert.Contains(TiposMensagem.DATA, ex.Message);
        Assert.Equal("sensorId", ex.ParamName);
    }

    [Fact]
    public void Construtor_TimestampInvalido_MensagemErroContemNomeCampo()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            new Mensagem(TiposMensagem.DATA_ACK, "", EmptyPayload, "invalido"));

        Assert.Equal("timestamp", ex.ParamName);
    }

    // ─── Construção válida preserva todos os campos ──────────────────────────────

    [Fact]
    public void Construtor_DadosValidos_PreservaTodosOsCampos()
    {
        var payload = new Dictionary<string, object> { ["chave"] = "valor" };
        var mensagem = new Mensagem(TiposMensagem.DATA, ValidSensorId, payload, ValidTimestamp);

        Assert.Equal(TiposMensagem.DATA, mensagem.Tipo);
        Assert.Equal(ValidSensorId, mensagem.SensorId);
        Assert.Equal(ValidTimestamp, mensagem.Timestamp);
        Assert.Equal("valor", mensagem.Payload["chave"]);
    }

    [Fact]
    public void Construtor_PayloadNulo_SubstituidoPorDicionarioVazio()
    {
        var mensagem = new Mensagem(TiposMensagem.DATA_ACK, "", null, ValidTimestamp);
        Assert.NotNull(mensagem.Payload);
        Assert.Empty(mensagem.Payload);
    }
}
