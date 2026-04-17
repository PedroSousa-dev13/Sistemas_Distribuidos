using SharedProtocol;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace Sensor;

public class SensorClient : IDisposable
{
    public string GatewayIp { get; }
    public int GatewayPort { get; }
    public string SensorId { get; }

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private readonly object _streamLock = new();

    private CancellationTokenSource? _cts;
    private Task? _heartbeatTask;

    // Lista dinâmica — será substituída pelos tipos enviados pela Gateway
    public static readonly List<string> TiposDadosSuportados = new()
    {
        "temperatura",
        "humidade",
        "qualidade_ar",
        "ruido",
        "pm25",
        "pm10",
        "luminosidade",
        "imagem"
    };

    public event EventHandler<string>? OnLog;

    public SensorClient(string gatewayIp, int gatewayPort, string sensorId)
    {
        GatewayIp = gatewayIp;
        GatewayPort = gatewayPort;
        SensorId = sensorId;
    }

    public async Task<bool> IniciarAsync()
    {
        _cts = new CancellationTokenSource();

        try
        {
            await ConectarAsync();

            if (!await RegistrarAsync())
            {
                Log("Registo falhou.");
                return false;
            }

            _heartbeatTask = Task.Run(() => HeartbeatLoopAsync(_cts.Token));
            return true;
        }
        catch (Exception ex)
        {
            Log($"Erro ao iniciar: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> EnviarMedicaoAsync(string tipoDado, object valor)
    {
        try
        {
            var mensagem = Mensagem.CriarData(SensorId, tipoDado, valor);
            await EnviarMensagemAsync(mensagem);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var resposta = await ReceberMensagemAsync(cts.Token);

            if (resposta?.Tipo == TiposMensagem.DATA_ACK)
            {
                Log($"Medição de {tipoDado} enviada com sucesso.");
                return true;
            }
            else if (resposta?.Tipo == TiposMensagem.ERROR)
            {
                var errorCode = resposta.Payload.GetValueOrDefault("error_code", "UNKNOWN")?.ToString();
                Log($"Erro da Gateway: {errorCode}");
                return false;
            }
            else
            {
                Log("Não foi recebida confirmação da Gateway.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Log($"Erro ao enviar medição: {ex.Message}");
            return false;
        }
    }

    public async Task PararAsync()
    {
        _cts?.Cancel();

        if (_heartbeatTask != null)
        {
            try { await _heartbeatTask.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { }
        }

        try { _stream?.Close(); } catch { }
        try { _tcpClient?.Close(); } catch { }

        Log("Sensor terminado.");
    }

    private async Task ConectarAsync()
    {
        Log($"A conectar a {GatewayIp}:{GatewayPort}...");

        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(GatewayIp, GatewayPort);
            _stream = _tcpClient.GetStream();

            Log("Conectado com sucesso!");
        }
        catch (SocketException ex)
        {
            throw new Exception($"Não foi possível conectar à Gateway: {ex.Message}");
        }
    }

    private async Task<bool> RegistrarAsync()
    {
        Log("A enviar pedido de registo...");

        var mensagem = Mensagem.CriarRegister(SensorId, TiposDadosSuportados);

        try
        {
            await EnviarMensagemAsync(mensagem);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var resposta = await ReceberMensagemAsync(cts.Token);

            if (resposta == null)
            {
                Log("Timeout ao aguardar resposta de registo.");
                return false;
            }

            switch (resposta.Tipo)
            {
                case TiposMensagem.REGISTER_OK:

                    // CORREÇÃO: interpretar corretamente o array vindo da Gateway
                    if (resposta.Payload != null &&
                        resposta.Payload.TryGetValue("tipos_dados", out var tiposObj))
                    {
                        try
                        {
                            var lista = JsonSerializer.Deserialize<List<string>>(tiposObj.ToString());

                            if (lista != null && lista.Count > 0)
                            {
                                TiposDadosSuportados.Clear();
                                TiposDadosSuportados.AddRange(lista);

                                Log($"Tipos de dados recebidos da Gateway: {string.Join(", ", lista)}");
                            }
                        }
                        catch
                        {
                            Log("Falha ao interpretar tipos_dados. Mantendo lista padrão.");
                        }
                    }

                    Log("Sensor registado na Gateway.");
                    return true;

                case TiposMensagem.REGISTER_ERR:
                    var errorCode = resposta.Payload.GetValueOrDefault("error_code", "UNKNOWN")?.ToString();
                    var description = resposta.Payload.GetValueOrDefault("description", "Erro desconhecido")?.ToString();
                    Log($"Erro de registo: {errorCode} - {description}");
                    return false;

                default:
                    Log($"Resposta inesperada: {resposta.Tipo}");
                    return false;
            }
        }
        catch (Exception ex)
        {
            Log($"Erro no registo: {ex.Message}");
            return false;
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);
                if (cancellationToken.IsCancellationRequested) break;

                await EnviarHeartbeatAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Erro no heartbeat: {ex.Message}");
            }
        }
    }

    private Task EnviarHeartbeatAsync()
    {
        try
        {
            var mensagem = Mensagem.CriarHeartbeat(SensorId);
            var json = MensagemSerializer.Serializar(mensagem);
            var dados = Encoding.UTF8.GetBytes(json + "\n");

            lock (_streamLock)
            {
                if (_stream?.CanWrite == true)
                    _stream.Write(dados, 0, dados.Length);
            }
        }
        catch (Exception ex)
        {
            Log($"Falha ao enviar heartbeat: {ex.Message}");
        }

        return Task.CompletedTask;
    }

    private async Task EnviarMensagemAsync(Mensagem mensagem)
    {
        lock (_streamLock)
        {
            if (_stream == null || !_stream.CanWrite)
                throw new InvalidOperationException("Stream não está disponível para escrita.");

            var json = MensagemSerializer.Serializar(mensagem);
            var dados = Encoding.UTF8.GetBytes(json + "\n");

            _stream.Write(dados, 0, dados.Length);
            Log($"Enviado: {json}");
        }

        await Task.CompletedTask;
    }

    private async Task<Mensagem?> ReceberMensagemAsync(CancellationToken cancellationToken)
    {
        if (_stream == null || !_stream.CanRead)
            return null;

        using var ms = new MemoryStream();
        var buffer = new byte[1024];

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                int bytesLidos;

                lock (_streamLock)
                {
                    if (!_stream.CanRead) return null;
                    bytesLidos = _stream.Read(buffer, 0, buffer.Length);
                }

                if (bytesLidos == 0)
                    return null;

                ms.Write(buffer, 0, bytesLidos);

                var dados = ms.ToArray();
                var newlineIndex = Array.IndexOf(dados, (byte)'\n');

                if (newlineIndex >= 0)
                {
                    var mensagemBytes = dados.Take(newlineIndex).ToArray();
                    var json = Encoding.UTF8.GetString(mensagemBytes);

                    var resto = dados.Skip(newlineIndex + 1).ToArray();
                    ms.SetLength(0);
                    if (resto.Length > 0)
                        ms.Write(resto, 0, resto.Length);

                    Log($"Recebido: {json}");
                    return MensagemSerializer.Deserializar(json);
                }
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                Log($"Erro ao receber: {ex.Message}");
                return null;
            }

            await Task.Delay(10, cancellationToken);
        }

        return null;
    }

    private void Log(string mensagem)
    {
        OnLog?.Invoke(this, mensagem);
    }

    public void Dispose()
    {
        PararAsync().Wait(TimeSpan.FromSeconds(2));
        _cts?.Dispose();
    }
}
