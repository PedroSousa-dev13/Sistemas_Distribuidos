namespace SharedProtocol;

/// <summary>
/// Define os tipos de mensagem suportados pelo protocolo.
/// </summary>
public static class TiposMensagem
{
    public const string REGISTER = "REGISTER";
    public const string REGISTER_OK = "REGISTER_OK";
    public const string REGISTER_ERR = "REGISTER_ERR";
    public const string DATA = "DATA";
    public const string DATA_ACK = "DATA_ACK";
    public const string HEARTBEAT = "HEARTBEAT";
    public const string HEARTBEAT_ACK = "HEARTBEAT_ACK";
    public const string ERROR = "ERROR";

    /// <summary>
    /// Conjunto de todos os tipos de mensagem válidos.
    /// </summary>
    public static readonly HashSet<string> Validos = new HashSet<string>
    {
        REGISTER, REGISTER_OK, REGISTER_ERR,
        DATA, DATA_ACK,
        HEARTBEAT, HEARTBEAT_ACK,
        ERROR
    };

    /// <summary>
    /// Determina se um tipo de mensagem requer sensor_id obrigatório.
    /// </summary>
    /// <param name="tipo">O tipo de mensagem a verificar.</param>
    /// <returns>True se o tipo requer sensor_id, false caso contrário.</returns>
    public static bool RequerSensorId(string tipo)
    {
        return tipo == DATA || tipo == HEARTBEAT || tipo == REGISTER;
    }
}
