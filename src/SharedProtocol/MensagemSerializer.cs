using System.Text.Json;

namespace SharedProtocol;

/// <summary>
/// Serializa e deserializa objetos Mensagem para/de formato JSON.
/// Thread-safe (stateless).
/// </summary>
public static class MensagemSerializer
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// Serializa um objeto Mensagem para uma string JSON.
    /// </summary>
    /// <param name="mensagem">A mensagem a serializar. Não pode ser nula.</param>
    /// <returns>Representação JSON da mensagem.</returns>
    /// <exception cref="ArgumentNullException">Lançada quando mensagem é nula.</exception>
    public static string Serializar(Mensagem mensagem)
    {
        if (mensagem == null)
            throw new ArgumentNullException(nameof(mensagem));

        return JsonSerializer.Serialize(mensagem, Options);
    }

    /// <summary>
    /// Deserializa uma string JSON para um objeto Mensagem.
    /// </summary>
    /// <param name="json">A string JSON a deserializar. Não pode ser nula ou vazia.</param>
    /// <returns>Objeto Mensagem deserializado.</returns>
    /// <exception cref="ArgumentException">Lançada quando json é nulo ou vazio, ou quando tipo, sensor_id ou timestamp são inválidos após o parse.</exception>
    /// <exception cref="FormatException">Lançada quando o JSON é inválido ou malformado.</exception>
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
