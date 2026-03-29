namespace SharedProtocol;

/// <summary>
/// Define os portos padrão do protocolo.
/// </summary>
public static class PortosProtocolo
{
    /// <summary>
    /// Porto padrão para comunicação com a Gateway (sensor-to-gateway).
    /// </summary>
    public const int GATEWAY_PORT = 5000;

    /// <summary>
    /// Porto padrão para comunicação com o Servidor (gateway-to-server).
    /// </summary>
    public const int SERVER_PORT = 6000;
}
