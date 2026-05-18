using SharedProtocol;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Gateway;

/// <summary>
/// Cliente RabbitMQ para subscrever e processar medições de sensores.
/// Substitui a conexão TCP direta com sensores.
/// </summary>
public class RabbitMQGatewayClient : IDisposable
{
    public string GatewayId { get; }
    public string RabbitMQHost { get; }
    public int RabbitMQPort { get; }

    private IConnection? _connection;
    private IChannel? _channel;
    private readonly object _channelLock = new();

    private Dictionary<string, SensorInfo> _sensors;
    private ConcurrentQueue<Mensagem> _messageQueue;
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;

    public event EventHandler<Mensagem>? OnMensagemRecebida;
    public event EventHandler<string>? OnLog;

    public RabbitMQGatewayClient(
        string gatewayId,
        Dictionary<string, SensorInfo> sensors,
        string rabbitMQHost = "localhost",
        int rabbitMQPort = 5672)
    {
        GatewayId = gatewayId;
        RabbitMQHost = rabbitMQHost;
        RabbitMQPort = rabbitMQPort;
        _sensors = sensors;
        _messageQueue = new ConcurrentQueue<Mensagem>();
    }

    public async Task<bool> IniciarAsync()
    {
        _cts = new CancellationTokenSource();

        try
        {
            await ConectarAsync();
            await DeclararExchangesEQueuesAsync();
            await IniciarConsumidoresAsync();

            _consumerTask = Task.Run(() => ProcessarFilaAsync(_cts.Token));

            return true;
        }
        catch (Exception ex)
        {
            Log($"Erro ao iniciar: {ex.Message}");
            return false;
        }
    }

    public void EnqueueMensagem(Mensagem mensagem)
    {
        _messageQueue.Enqueue(mensagem);
    }

    public async Task PararAsync()
    {
        _cts?.Cancel();

        if (_consumerTask != null)
        {
            try { await _consumerTask.WaitAsync(TimeSpan.FromSeconds(2)); }
            catch { }
        }

        try { if (_channel != null) await _channel.CloseAsync(); } catch { }
        try { if (_connection != null) await _connection.CloseAsync(); } catch { }

        Log("Gateway RabbitMQ desligado.");
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
                UserName = "guest",
                Password = "guest",
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

            // ─── Exchange para medições ───
            await _channel.ExchangeDeclareAsync(
                "sensor-measurements",
                ExchangeType.Topic,
                durable: true,
                autoDelete: false
            );

            // ─── Exchange para controle ───
            await _channel.ExchangeDeclareAsync(
                "sensor-control",
                ExchangeType.Direct,
                durable: true,
                autoDelete: false
            );

            // ─── Fila para medições gerais ───
            await _channel.QueueDeclareAsync(
                queue: $"gateway-measurements-{GatewayId}",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // ─── Fila para eventos de controle ───
            await _channel.QueueDeclareAsync(
                queue: $"gateway-control-{GatewayId}",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null
            );

            // ─── Bindings para medições (subscribe a todos os sensores) ───
            await _channel.QueueBindAsync(
                queue: $"gateway-measurements-{GatewayId}",
                exchange: "sensor-measurements",
                routingKey: "sensor.*.#",  // Qualquer sensor, qualquer tipo de dado
                arguments: null
            );

            // ─── Bindings para controle ───
            await _channel.QueueBindAsync(
                queue: $"gateway-control-{GatewayId}",
                exchange: "sensor-control",
                routingKey: "#",  // Qualquer evento de controle
                arguments: null
            );

            Log("Exchanges e queues declarados com sucesso");
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao declarar exchanges/queues: {ex.Message}");
        }
    }

    private async Task IniciarConsumidoresAsync()
    {
        try
        {
            if (_channel == null)
                throw new InvalidOperationException("Canal não inicializado");

            // Consumidor para medições
            var consumer1 = new AsyncEventingBasicConsumer(_channel);
            consumer1.ReceivedAsync += async (model, ea) => await ProcessarMensagemAsync(ea);

            await _channel.BasicConsumeAsync(
                queue: $"gateway-measurements-{GatewayId}",
                autoAck: false,
                consumerTag: $"gateway-measurements-consumer",
                consumer: consumer1
            );

            // Consumidor para controle
            var consumer2 = new AsyncEventingBasicConsumer(_channel);
            consumer2.ReceivedAsync += async (model, ea) => await ProcessarMensagemAsync(ea);

            await _channel.BasicConsumeAsync(
                queue: $"gateway-control-{GatewayId}",
                autoAck: false,
                consumerTag: $"gateway-control-consumer",
                consumer: consumer2
            );

            Log("Consumidores iniciados");
        }
        catch (Exception ex)
        {
            throw new Exception($"Erro ao iniciar consumidores: {ex.Message}");
        }
    }

    private async Task ProcessarMensagemAsync(BasicDeliverEventArgs ea)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            var mensagem = JsonSerializer.Deserialize<Mensagem>(json);

            if (mensagem != null)
            {
                Log($"Mensagem recebida - Tipo: {mensagem.Tipo}, Sensor: {mensagem.SensorId}, " +
                    $"RoutingKey: {ea.RoutingKey}");

                EnqueueMensagem(mensagem);

                // ACK após enqueue bem-sucedido
                await _channel!.BasicAckAsync(ea.DeliveryTag, multiple: false);
            }
        }
        catch (Exception ex)
        {
            Log($"Erro ao processar mensagem: {ex.Message}");
            // NACK para requeue
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true);
        }
    }

    private async Task ProcessarFilaAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_messageQueue.TryDequeue(out var mensagem))
                {
                    OnMensagemRecebida?.Invoke(this, mensagem);
                    await Task.Delay(10, cancellationToken);
                }
                else
                {
                    await Task.Delay(100, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log($"Erro ao processar fila: {ex.Message}");
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
