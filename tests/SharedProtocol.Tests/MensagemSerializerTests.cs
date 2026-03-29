using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;
using SharedProtocol;

namespace SharedProtocol.Tests;

/// <summary>
/// Testes unitários para MensagemSerializer.
/// Validates: Requirements 3.1-3.5, 4.1-4.5
/// </summary>
public class MensagemSerializerTests
{
    private static readonly string ValidTimestamp = "2024-01-15T10:30:00.0000000Z";
    private const string SensorId = "SENSOR_001";

    // ─── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Compares payload values accounting for JsonElement deserialization.
    /// </summary>
    private static bool PayloadValueEquals(object? expected, object? actual)
    {
        if (expected == null && actual == null) return true;
        if (expected == null || actual == null) return false;

        if (actual is JsonElement element)
        {
            return element.ToString() == expected.ToString();
        }
        return expected.Equals(actual);
    }

    // ─── Serialização ────────────────────────────────────────────────────────────

    [Fact]
    public void Serializar_MensagemNula_LancaArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => MensagemSerializer.Serializar(null!));
    }

    [Fact]
    public void Serializar_MensagemRegister_ProduziJsonValido()
    {
        var msg = Mensagem.CriarRegister(SensorId, new List<string> { "temperatura" });
        var json = MensagemSerializer.Serializar(msg);
        Assert.False(string.IsNullOrWhiteSpace(json));
        // Must be parseable JSON
        var doc = JsonDocument.Parse(json);
        Assert.NotNull(doc);
    }

    [Fact]
    public void Serializar_MensagemRegister_ContemCamposTipo()
    {
        var msg = Mensagem.CriarRegister(SensorId, new List<string> { "temperatura" });
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"tipo\"", json);
        Assert.Contains("\"REGISTER\"", json);
    }

    [Fact]
    public void Serializar_MensagemRegister_ContemSensorId()
    {
        var msg = Mensagem.CriarRegister(SensorId, new List<string> { "temperatura" });
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"sensor_id\"", json);
        Assert.Contains(SensorId, json);
    }

    [Fact]
    public void Serializar_MensagemRegister_ContemTimestamp()
    {
        var msg = Mensagem.CriarRegister(SensorId, new List<string> { "temperatura" });
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"timestamp\"", json);
    }

    [Fact]
    public void Serializar_MensagemData_ContemPayload()
    {
        var msg = Mensagem.CriarData(SensorId, "temperatura", 23.5);
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"payload\"", json);
        Assert.Contains("temperatura", json);
    }

    [Fact]
    public void Serializar_MensagemHeartbeat_ProduziJsonValido()
    {
        var msg = Mensagem.CriarHeartbeat(SensorId);
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"HEARTBEAT\"", json);
    }

    [Fact]
    public void Serializar_MensagemRegisterOk_ProduziJsonValido()
    {
        var msg = Mensagem.CriarRegisterOk(SensorId);
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"REGISTER_OK\"", json);
    }

    [Fact]
    public void Serializar_MensagemRegisterErr_ProduziJsonValido()
    {
        var msg = Mensagem.CriarRegisterErr(SensorId, CodigosErro.SENSOR_NOT_FOUND, "Sensor não encontrado");
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"REGISTER_ERR\"", json);
        Assert.Contains("SENSOR_NOT_FOUND", json);
    }

    [Fact]
    public void Serializar_MensagemError_ProduziJsonValido()
    {
        var msg = Mensagem.CriarError(SensorId, CodigosErro.SERVER_UNAVAILABLE, "Servidor indisponível");
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"ERROR\"", json);
        Assert.Contains("SERVER_UNAVAILABLE", json);
    }

    [Fact]
    public void Serializar_MensagemDataAck_ProduziJsonValido()
    {
        var msg = Mensagem.CriarDataAck(SensorId);
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"DATA_ACK\"", json);
    }

    [Fact]
    public void Serializar_MensagemHeartbeatAck_ProduziJsonValido()
    {
        var msg = Mensagem.CriarHeartbeatAck(SensorId);
        var json = MensagemSerializer.Serializar(msg);
        Assert.Contains("\"HEARTBEAT_ACK\"", json);
    }

    [Fact]
    public void Serializar_PayloadNulo_NaoLancaExcecao()
    {
        // REGISTER_OK has empty payload (null passed to constructor)
        var msg = Mensagem.CriarRegisterOk(SensorId);
        var json = MensagemSerializer.Serializar(msg);
        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void Serializar_PayloadVazio_NaoLancaExcecao()
    {
        var msg = new Mensagem(TiposMensagem.DATA_ACK, SensorId, new Dictionary<string, object>(), ValidTimestamp);
        var json = MensagemSerializer.Serializar(msg);
        Assert.False(string.IsNullOrWhiteSpace(json));
    }

    [Fact]
    public void Serializar_CaracteresEspeciais_PreservadosNoJson()
    {
        var msg = Mensagem.CriarRegisterErr(SensorId, CodigosErro.SENSOR_NOT_FOUND, "Sensor não encontrado: ação inválida");
        var json = MensagemSerializer.Serializar(msg);
        // JSON should contain the special characters (possibly escaped)
        Assert.False(string.IsNullOrWhiteSpace(json));
        var doc = JsonDocument.Parse(json);
        var payload = doc.RootElement.GetProperty("payload");
        var desc = payload.GetProperty("description").GetString();
        Assert.Equal("Sensor não encontrado: ação inválida", desc);
    }

    [Fact]
    public void Serializar_NaoUsaIndentacao()
    {
        var msg = Mensagem.CriarHeartbeat(SensorId);
        var json = MensagemSerializer.Serializar(msg);
        Assert.DoesNotContain("\n", json);
    }

    // ─── Deserialização ──────────────────────────────────────────────────────────

    [Fact]
    public void Deserializar_JsonNulo_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MensagemSerializer.Deserializar(null!));
    }

    [Fact]
    public void Deserializar_JsonVazio_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MensagemSerializer.Deserializar(""));
    }

    [Fact]
    public void Deserializar_JsonEspacos_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(() => MensagemSerializer.Deserializar("   "));
    }

    [Fact]
    public void Deserializar_JsonInvalido_LancaFormatException()
    {
        Assert.Throws<FormatException>(() => MensagemSerializer.Deserializar("isto não é json"));
    }

    [Fact]
    public void Deserializar_JsonMalformado_LancaFormatException()
    {
        Assert.Throws<FormatException>(() => MensagemSerializer.Deserializar("{\"tipo\": \"DATA\""));
    }

    [Fact]
    public void Deserializar_TipoDesconhecido_LancaArgumentException()
    {
        var json = "{\"tipo\":\"UNKNOWN_PROTO_MSG\",\"sensor_id\":\"SENSOR_001\",\"payload\":{},\"timestamp\":\"2024-01-15T10:30:00.0000000Z\"}";
        var ex = Assert.Throws<ArgumentException>(() => MensagemSerializer.Deserializar(json));
        Assert.Contains("não é válido", ex.Message);
    }

    [Fact]
    public void Deserializar_JsonValido_RetornaMensagem()
    {
        var json = "{\"tipo\":\"HEARTBEAT_ACK\",\"sensor_id\":\"SENSOR_001\",\"payload\":{},\"timestamp\":\"2024-01-15T10:30:00.0000000Z\"}";
        var msg = MensagemSerializer.Deserializar(json);
        Assert.NotNull(msg);
        Assert.Equal("HEARTBEAT_ACK", msg.Tipo);
        Assert.Equal("SENSOR_001", msg.SensorId);
        Assert.Equal("2024-01-15T10:30:00.0000000Z", msg.Timestamp);
    }

    [Fact]
    public void Deserializar_PreservaTipo()
    {
        var original = Mensagem.CriarHeartbeat(SensorId);
        var json = MensagemSerializer.Serializar(original);
        var deserialized = MensagemSerializer.Deserializar(json);
        Assert.Equal(original.Tipo, deserialized.Tipo);
    }

    [Fact]
    public void Deserializar_PreservaSensorId()
    {
        var original = Mensagem.CriarHeartbeat(SensorId);
        var json = MensagemSerializer.Serializar(original);
        var deserialized = MensagemSerializer.Deserializar(json);
        Assert.Equal(original.SensorId, deserialized.SensorId);
    }

    [Fact]
    public void Deserializar_PreservaTimestamp()
    {
        var original = Mensagem.CriarHeartbeat(SensorId);
        var json = MensagemSerializer.Serializar(original);
        var deserialized = MensagemSerializer.Deserializar(json);
        Assert.Equal(original.Timestamp, deserialized.Timestamp);
    }

    [Fact]
    public void Deserializar_PreservaPayloadChaves()
    {
        var original = Mensagem.CriarData(SensorId, "temperatura", 23.5);
        var json = MensagemSerializer.Serializar(original);
        var deserialized = MensagemSerializer.Deserializar(json);
        Assert.True(deserialized.Payload.ContainsKey("tipo_dado"));
        Assert.True(deserialized.Payload.ContainsKey("valor"));
    }

    [Fact]
    public void Deserializar_PreservaPayloadValores()
    {
        var original = Mensagem.CriarData(SensorId, "temperatura", 23.5);
        var json = MensagemSerializer.Serializar(original);
        var deserialized = MensagemSerializer.Deserializar(json);
        Assert.True(PayloadValueEquals("temperatura", deserialized.Payload["tipo_dado"]));
    }

    [Fact]
    public void Deserializar_CaracteresEspeciais_Preservados()
    {
        var original = Mensagem.CriarRegisterErr(SensorId, CodigosErro.SENSOR_NOT_FOUND, "Sensor não encontrado: ação inválida");
        var json = MensagemSerializer.Serializar(original);
        var deserialized = MensagemSerializer.Deserializar(json);
        Assert.True(PayloadValueEquals("Sensor não encontrado: ação inválida", deserialized.Payload["description"]));
    }

    [Fact]
    public void Deserializar_PayloadVazio_NaoLancaExcecao()
    {
        var json = "{\"tipo\":\"DATA_ACK\",\"sensor_id\":\"SENSOR_001\",\"payload\":{},\"timestamp\":\"2024-01-15T10:30:00.0000000Z\"}";
        var msg = MensagemSerializer.Deserializar(json);
        Assert.NotNull(msg);
        Assert.Empty(msg.Payload);
    }
}
