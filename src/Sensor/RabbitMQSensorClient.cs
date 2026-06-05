using SharedProtocol;
using RabbitMQ.Client;
using System;
using System.Text;
using System.Text.Json;

namespace Sensor;

/// <summary>
/// Cliente RabbitMQ para publicar medições de sensores.
/// Substitui a conexão TCP direta com a Gateway.
/// </summary>
public class RabbitMQSensorClient : IDisposable
{
    public string SensorId { get; }
    public string RabbitMQHost { get; }
    public int RabbitMQPort { get; }
    public string Zona { get; set; } = "";

    private IConnection? _connection;
    private IChannel? _channel;

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

    public RabbitMQSensorClient(string sensorId, string rabbitMQHost = "localhost", int rabbitMQPort = 5672)
    {
        SensorId = sensorId;
        RabbitMQHost = rabbitMQHost;
        RabbitMQPort = rabbitMQPort;
    }

    public async Task<bool> IniciarAsync()
    {
        _cts = new CancellationTokenSource();

        try
        {
            await ConectarAsync();
            await DeclararExchangesEQueuesAsync();
            
            // Enviar mensagem de registo
            await RegistrarAsync();
            
            // Iniciar heartbeat
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
            await PublicarMensagemAsync(mensagem);

            Log($"Medição de {tipoDado} publicada com sucesso.");
            return true;
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

        try { if (_channel != null) await _channel.CloseAsync(); } catch { }
        try { if (_connection != null) await _connection.CloseAsync(); } catch { }

        Log("Sensor terminado.");
    }

    private async Task ConectarAsync()
    {
        Log($"A conectar a RabbitMQ em {RabbitMQHost}:{RabbitMQPort}...");

        try
        {
            var factory = new ConnectionFactory
            {
                HostName = RabbitMQHost,
                Port = RabbitMQPort,
                UserName = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest",
                Password = Environment.GetEnvironmentVariable("RABBITMQ_PASS") ?? "guest",
                AutomaticRecoveryEnabled = true,
                RequestedHeartbeat = TimeSpan.FromSeconds(30)
            };

            _connection = await factory.CreateConnectionAsync();
            _channel = await _connection.CreateChannelAsync();

            Log("Conectado a RabbitMQ com sucesso!");
        }
        catch (Exception ex)
        {
            throw new Exception($"Não foi possível conectar a RabbitMQ: {ex.Message}");
        }
    }

    private async Task DeclararExchangesEQueuesAsync()
    {
        try
        {
            if (_channel == null)
                throw new InvalidOperationException("Canal não inicializado");

            // Exchange type Topic para routing mais flexível
            await _channel.ExchangeDeclareAsync(
                "sensor-measurements",           // exchange name
                ExchangeType.Topic,              // type
                durable: true,
                autoDelete: false
            );

            // Exchange para mensagens de controle (registro, heartbeat)
            await _channel.ExchangeDeclareAsync(
                "sensor-control",
                ExchangeType.Topic,
                durable: true,
                autoDelete: false
            );

            Log("Exchanges declarados com sucesso");
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao declarar exchanges: {ex.Message}");
        }
    }

    private async Task RegistrarAsync()
    {
        Log("A publicar mensagem de registo...");

        var mensagem = Mensagem.CriarRegister(SensorId, TiposDadosSuportados);
        
        try
        {
            await PublicarMensagemAsync(mensagem, "sensor-control", "register");
            Log("Registo publicado com sucesso");
        }
        catch (Exception ex)
        {
            Log($"Erro ao publicar registo: {ex.Message}");
            throw;
        }
    }

    private async Task PublicarMensagemAsync(Mensagem mensagem, string exchangeName = "sensor-measurements", 
                                            string routingKey = "")
    {
        try
        {
            if (_channel == null)
                throw new InvalidOperationException("Canal não inicializado");

            if (string.IsNullOrEmpty(routingKey))
            {
                var tipoDado = mensagem.Payload.GetValueOrDefault("tipo_dado", "unknown")?.ToString() ?? "unknown";
                routingKey = $"sensor.{SensorId}.{tipoDado}";
            }

            var json = JsonSerializer.Serialize(mensagem);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await _channel.BasicPublishAsync(
                exchange: exchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: properties,
                body: body
            );

            Log($"Mensagem publicada - Exchange: {exchangeName}, RoutingKey: {routingKey}");

            if (!string.IsNullOrEmpty(Zona) && exchangeName == "sensor-measurements")
            {
                var tipoDado = mensagem.Payload.GetValueOrDefault("tipo_dado", "unknown")?.ToString() ?? "unknown";

                var zoneRoutingKey = $"zona.{Zona}.{tipoDado}";

                await _channel.BasicPublishAsync(
                    exchange: exchangeName,
                    routingKey: zoneRoutingKey,
                    mandatory: false,
                    basicProperties: properties,
                    body: body
                );

                Log($"Mensagem tambem publicada com routing por zona: {zoneRoutingKey}");
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao publicar mensagem: {ex.Message}");
        }
    }

    private async Task HeartbeatLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

                var heartbeat = new Mensagem(
                    TiposMensagem.HEARTBEAT,
                    SensorId,
                    new Dictionary<string, object> { ["status"] = "online" },
                    DateTime.UtcNow.ToString("o")
                );

                await PublicarMensagemAsync(heartbeat, "sensor-control", "heartbeat");
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

    private void Log(string message)
    {
        OnLog?.Invoke(this, message);
    }

    public void Dispose()
    {
        _cts?.Dispose();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
