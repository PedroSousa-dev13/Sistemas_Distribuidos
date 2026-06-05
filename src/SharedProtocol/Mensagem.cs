using System.Text.Json.Serialization;

namespace SharedProtocol;

public class Mensagem
{
    [JsonPropertyName("tipo")]
    public string Tipo { get; init; } = string.Empty;

    [JsonPropertyName("sensor_id")]
    public string SensorId { get; init; } = string.Empty;

    [JsonPropertyName("payload")]
    public Dictionary<string, object> Payload { get; init; } = new();

    [JsonPropertyName("timestamp")]
    public string Timestamp { get; init; } = string.Empty;


    public Mensagem(string tipo, string sensorId, Dictionary<string, object>? payload, string timestamp)
    {
        if (string.IsNullOrWhiteSpace(tipo))
            throw new ArgumentException("Tipo não pode ser nulo ou vazio", nameof(tipo));

        if (!TiposMensagem.Validos.Contains(tipo))
            throw new ArgumentException($"Tipo '{tipo}' não é válido", nameof(tipo));

        if (TiposMensagem.RequerSensorId(tipo) && string.IsNullOrWhiteSpace(sensorId))
            throw new ArgumentException($"SensorId é obrigatório para mensagens do tipo '{tipo}'", nameof(sensorId));

        if (string.IsNullOrWhiteSpace(timestamp) ||
            !DateTime.TryParseExact(timestamp, new[]
            {
                "yyyy-MM-ddTHH:mm:ssK",
                "yyyy-MM-ddTHH:mm:ss.fK",
                "yyyy-MM-ddTHH:mm:ss.ffK",
                "yyyy-MM-ddTHH:mm:ss.fffK",
                "yyyy-MM-ddTHH:mm:ss.ffffK",
                "yyyy-MM-ddTHH:mm:ss.fffffK",
                "yyyy-MM-ddTHH:mm:ss.ffffffK",
                "yyyy-MM-ddTHH:mm:ss.fffffffK",
                "o", "O"
            }, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.RoundtripKind, out _))
            throw new ArgumentException("Timestamp deve estar em formato ISO 8601 válido", nameof(timestamp));

        Tipo = tipo;
        SensorId = sensorId ?? string.Empty;
        Payload = payload ?? new Dictionary<string, object>();
        Timestamp = timestamp;
    }

    [JsonConstructor]
    public Mensagem() { }

    public static Mensagem CriarRegister(string sensorId, List<string> tiposDados)
    {
        var payload = new Dictionary<string, object> { ["tipos_dados"] = tiposDados };
        return new Mensagem(TiposMensagem.REGISTER, sensorId, payload, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarRegisterOk(string sensorId)
        => new Mensagem(TiposMensagem.REGISTER_OK, sensorId, null, DateTime.UtcNow.ToString("o"));

    public static Mensagem CriarRegisterErr(string sensorId, string errorCode, string description)
    {
        var payload = new Dictionary<string, object>
        {
            ["error_code"] = errorCode,
            ["description"] = description
        };
        return new Mensagem(TiposMensagem.REGISTER_ERR, sensorId, payload, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarData(string sensorId, string tipoDado, object valor)
    {
        var payload = new Dictionary<string, object>
        {
            ["tipo_dado"] = tipoDado,
            ["valor"] = valor
        };
        return new Mensagem(TiposMensagem.DATA, sensorId, payload, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarDataAck(string sensorId)
        => new Mensagem(TiposMensagem.DATA_ACK, sensorId, null, DateTime.UtcNow.ToString("o"));

    public static Mensagem CriarHeartbeat(string sensorId)
        => new Mensagem(TiposMensagem.HEARTBEAT, sensorId, null, DateTime.UtcNow.ToString("o"));

    public static Mensagem CriarHeartbeatAck(string sensorId)
        => new Mensagem(TiposMensagem.HEARTBEAT_ACK, sensorId, null, DateTime.UtcNow.ToString("o"));

    public static Mensagem CriarError(string sensorId, string errorCode, string description)
    {
        var payload = new Dictionary<string, object>
        {
            ["error_code"] = errorCode,
            ["description"] = description
        };
        return new Mensagem(TiposMensagem.ERROR, sensorId, payload, DateTime.UtcNow.ToString("o"));
    }
}
