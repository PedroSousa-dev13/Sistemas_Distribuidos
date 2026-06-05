using System.Text.Json;

namespace SharedProtocol;

public static class MensagemSerializer
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serializar(Mensagem mensagem)
    {
        if (mensagem == null)
            throw new ArgumentNullException(nameof(mensagem));

        return JsonSerializer.Serialize(mensagem, Options);
    }

    public static Mensagem Deserializar(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON não pode ser nulo ou vazio", nameof(json));

        try
        {
            var raw = JsonSerializer.Deserialize<Mensagem>(json, Options);

            if (raw == null)
                throw new FormatException("Deserialização resultou em objeto nulo");

            return new Mensagem(raw.Tipo, raw.SensorId, raw.Payload, raw.Timestamp);
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Formato JSON inválido: {ex.Message}", ex);
        }
    }
}
