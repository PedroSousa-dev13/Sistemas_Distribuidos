using System;
using System.Collections.Generic;
using Xunit;
using SharedProtocol;

namespace SharedProtocol.Tests;

/// <summary>
/// Testes unitários para os factory methods da classe Mensagem.
/// Validates: Requirements 2.1-2.8, 11.1-11.10, 12.1-12.4
/// </summary>
public class FactoryMethodsTests
{
    private const string SensorId = "SENSOR_001";

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    private static bool IsIso8601(string timestamp)
        => DateTime.TryParseExact(timestamp,
            new[] { "o", "O", "yyyy-MM-ddTHH:mm:ssK", "yyyy-MM-ddTHH:mm:ss.fffffffK" },
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out _);

    // ─── CriarRegister ───────────────────────────────────────────────────────────

    [Fact]
    public void CriarRegister_TipoCorreto()
    {
        var msg = Mensagem.CriarRegister(SensorId, new List<string> { "temperatura" });
        Assert.Equal(TiposMensagem.REGISTER, msg.Tipo);
    }

    [Fact]
    public void CriarRegister_SensorIdCorreto()
    {
        var msg = Mensagem.CriarRegister(SensorId, new List<string> { "temperatura" });
        Assert.Equal(SensorId, msg.SensorId);
    }

    [Fact]
    public void CriarRegister_PayloadContemTiposDados()
    {
        var tipos = new List<string> { "temperatura", "humidade" };
        var msg = Mensagem.CriarRegister(SensorId, tipos);
        Assert.True(msg.Payload.ContainsKey("tipos_dados"));
        Assert.Equal(tipos, msg.Payload["tipos_dados"]);
    }

    [Fact]
    public void CriarRegister_TimestampIso8601()
    {
        var msg = Mensagem.CriarRegister(SensorId, new List<string> { "temperatura" });
        Assert.True(IsIso8601(msg.Timestamp));
    }

    // ─── CriarRegisterOk ─────────────────────────────────────────────────────────

    [Fact]
    public void CriarRegisterOk_TipoCorreto()
    {
        var msg = Mensagem.CriarRegisterOk(SensorId);
        Assert.Equal(TiposMensagem.REGISTER_OK, msg.Tipo);
    }

    [Fact]
    public void CriarRegisterOk_PayloadVazio()
    {
        var msg = Mensagem.CriarRegisterOk(SensorId);
        Assert.Empty(msg.Payload);
    }

    [Fact]
    public void CriarRegisterOk_TimestampIso8601()
    {
        var msg = Mensagem.CriarRegisterOk(SensorId);
        Assert.True(IsIso8601(msg.Timestamp));
    }

    // ─── CriarRegisterErr ────────────────────────────────────────────────────────

    [Fact]
    public void CriarRegisterErr_TipoCorreto()
    {
        var msg = Mensagem.CriarRegisterErr(SensorId, CodigosErro.SENSOR_NOT_FOUND, "Sensor não encontrado");
        Assert.Equal(TiposMensagem.REGISTER_ERR, msg.Tipo);
    }

    [Fact]
    public void CriarRegisterErr_PayloadContemErrorCode()
    {
        var msg = Mensagem.CriarRegisterErr(SensorId, CodigosErro.SENSOR_NOT_FOUND, "Sensor não encontrado");
        Assert.True(msg.Payload.ContainsKey("error_code"));
        Assert.Equal(CodigosErro.SENSOR_NOT_FOUND, msg.Payload["error_code"]);
    }

    [Fact]
    public void CriarRegisterErr_PayloadContemDescription()
    {
        const string desc = "Sensor não encontrado";
        var msg = Mensagem.CriarRegisterErr(SensorId, CodigosErro.SENSOR_NOT_FOUND, desc);
        Assert.True(msg.Payload.ContainsKey("description"));
        Assert.Equal(desc, msg.Payload["description"]);
    }

    [Fact]
    public void CriarRegisterErr_TimestampIso8601()
    {
        var msg = Mensagem.CriarRegisterErr(SensorId, CodigosErro.SENSOR_NOT_FOUND, "erro");
        Assert.True(IsIso8601(msg.Timestamp));
    }

    // ─── CriarHeartbeat ──────────────────────────────────────────────────────────

    [Fact]
    public void CriarHeartbeat_TipoCorreto()
    {
        var msg = Mensagem.CriarHeartbeat(SensorId);
        Assert.Equal(TiposMensagem.HEARTBEAT, msg.Tipo);
    }

    [Fact]
    public void CriarHeartbeat_PayloadVazio()
    {
        var msg = Mensagem.CriarHeartbeat(SensorId);
        Assert.Empty(msg.Payload);
    }

    [Fact]
    public void CriarHeartbeat_TimestampIso8601()
    {
        var msg = Mensagem.CriarHeartbeat(SensorId);
        Assert.True(IsIso8601(msg.Timestamp));
    }

    // ─── CriarHeartbeatAck ───────────────────────────────────────────────────────

    [Fact]
    public void CriarHeartbeatAck_TipoCorreto()
    {
        var msg = Mensagem.CriarHeartbeatAck(SensorId);
        Assert.Equal(TiposMensagem.HEARTBEAT_ACK, msg.Tipo);
    }

    [Fact]
    public void CriarHeartbeatAck_PayloadVazio()
    {
        var msg = Mensagem.CriarHeartbeatAck(SensorId);
        Assert.Empty(msg.Payload);
    }

    [Fact]
    public void CriarHeartbeatAck_TimestampIso8601()
    {
        var msg = Mensagem.CriarHeartbeatAck(SensorId);
        Assert.True(IsIso8601(msg.Timestamp));
    }

    // ─── CriarData ───────────────────────────────────────────────────────────────

    [Fact]
    public void CriarData_TipoCorreto()
    {
        var msg = Mensagem.CriarData(SensorId, "temperatura", 23.5);
        Assert.Equal(TiposMensagem.DATA, msg.Tipo);
    }

    [Fact]
    public void CriarData_PayloadContemTipoDado()
    {
        var msg = Mensagem.CriarData(SensorId, "temperatura", 23.5);
        Assert.True(msg.Payload.ContainsKey("tipo_dado"));
        Assert.Equal("temperatura", msg.Payload["tipo_dado"]);
    }

    [Fact]
    public void CriarData_PayloadContemValor()
    {
        var msg = Mensagem.CriarData(SensorId, "temperatura", 23.5);
        Assert.True(msg.Payload.ContainsKey("valor"));
        Assert.Equal(23.5, msg.Payload["valor"]);
    }

    [Fact]
    public void CriarData_TimestampIso8601()
    {
        var msg = Mensagem.CriarData(SensorId, "temperatura", 23.5);
        Assert.True(IsIso8601(msg.Timestamp));
    }

    // ─── CriarData — todos os tipos de dados de sensores (Req 11.1-11.8) ────────

    [Theory]
    [InlineData("temperatura", 22.5)]
    [InlineData("humidade", 65.0)]
    [InlineData("qualidade_ar", 42.0)]
    [InlineData("ruido", 55.3)]
    [InlineData("pm25", 12.1)]
    [InlineData("pm10", 18.4)]
    [InlineData("luminosidade", 300.0)]
    public void CriarData_TiposDadosNumericos_PayloadCorreto(string tipoDado, double valor)
    {
        var msg = Mensagem.CriarData(SensorId, tipoDado, valor);
        Assert.Equal(TiposMensagem.DATA, msg.Tipo);
        Assert.Equal(tipoDado, msg.Payload["tipo_dado"]);
        Assert.Equal(valor, msg.Payload["valor"]);
    }

    [Fact]
    public void CriarData_TipoDadoImagem_PayloadCorreto()
    {
        const string imagemUrl = "https://sensor.local/imagem.jpg";
        var msg = Mensagem.CriarData(SensorId, "imagem", imagemUrl);
        Assert.Equal(TiposMensagem.DATA, msg.Tipo);
        Assert.Equal("imagem", msg.Payload["tipo_dado"]);
        Assert.Equal(imagemUrl, msg.Payload["valor"]);
    }

    // ─── CriarDataAck ────────────────────────────────────────────────────────────

    [Fact]
    public void CriarDataAck_TipoCorreto()
    {
        var msg = Mensagem.CriarDataAck(SensorId);
        Assert.Equal(TiposMensagem.DATA_ACK, msg.Tipo);
    }

    [Fact]
    public void CriarDataAck_PayloadVazio()
    {
        var msg = Mensagem.CriarDataAck(SensorId);
        Assert.Empty(msg.Payload);
    }

    [Fact]
    public void CriarDataAck_TimestampIso8601()
    {
        var msg = Mensagem.CriarDataAck(SensorId);
        Assert.True(IsIso8601(msg.Timestamp));
    }

    // ─── CriarError ──────────────────────────────────────────────────────────────

    [Fact]
    public void CriarError_TipoCorreto()
    {
        var msg = Mensagem.CriarError(SensorId, CodigosErro.SERVER_UNAVAILABLE, "Servidor indisponível");
        Assert.Equal(TiposMensagem.ERROR, msg.Tipo);
    }

    [Fact]
    public void CriarError_PayloadContemErrorCode()
    {
        var msg = Mensagem.CriarError(SensorId, CodigosErro.SERVER_UNAVAILABLE, "Servidor indisponível");
        Assert.True(msg.Payload.ContainsKey("error_code"));
        Assert.Equal(CodigosErro.SERVER_UNAVAILABLE, msg.Payload["error_code"]);
    }

    [Fact]
    public void CriarError_PayloadContemDescription()
    {
        const string desc = "Servidor indisponível";
        var msg = Mensagem.CriarError(SensorId, CodigosErro.SERVER_UNAVAILABLE, desc);
        Assert.True(msg.Payload.ContainsKey("description"));
        Assert.Equal(desc, msg.Payload["description"]);
    }

    [Fact]
    public void CriarError_TimestampIso8601()
    {
        var msg = Mensagem.CriarError(SensorId, CodigosErro.SERVER_UNAVAILABLE, "erro");
        Assert.True(IsIso8601(msg.Timestamp));
    }
}
