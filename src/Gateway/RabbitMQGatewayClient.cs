using SharedProtocol;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;

namespace Gateway;

public class RabbitMQGatewayClient : IDisposable
{
    public string GatewayId { get; }
    public string RabbitMQHost { get; }
    public int RabbitMQPort { get; }

    private IConnection? _connection;
    private IChannel? _channel;

    private Dictionary<string, SensorInfo> _sensors;
    private ConcurrentQueue<QueuedMessage> _messageQueue;
    private CancellationTokenSource? _cts;
    private Task? _consumerTask;

    private const int MaxDeliveryRetries = 3;

    private class QueuedMessage
    {
        public Mensagem Message { get; set; } = null!;
        public ulong DeliveryTag { get; set; }
        public string Queue { get; set; } = "";
    }

    public event Func<object, Mensagem, Task>? OnMensagemRecebida;
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
        _messageQueue = new ConcurrentQueue<QueuedMessage>();
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

            await _channel.ExchangeDeclareAsync(
                "sensor-measurements",
                ExchangeType.Topic,
                durable: true,
                autoDelete: false
            );

            await _channel.ExchangeDeclareAsync(
                "sensor-control",
                ExchangeType.Direct,
                durable: true,
                autoDelete: false
            );

            var args = new Dictionary<string, object?>
            {
                { "x-delivery-limit", MaxDeliveryRetries }
            };

            await _channel.QueueDeclareAsync(
                queue: $"gateway-measurements-{GatewayId}",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: args
            );

            await _channel.QueueDeclareAsync(
                queue: $"gateway-control-{GatewayId}",
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: args
            );

            await _channel.QueueBindAsync(
                queue: $"gateway-measurements-{GatewayId}",
                exchange: "sensor-measurements",
                routingKey: "sensor.*.#",
                arguments: null
            );

            await _channel.QueueBindAsync(
                queue: $"gateway-measurements-{GatewayId}",
                exchange: "sensor-measurements",
                routingKey: "zona.*.#",
                arguments: null
            );

            await _channel.QueueBindAsync(
                queue: $"gateway-control-{GatewayId}",
                exchange: "sensor-control",
                routingKey: "#",
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

            var consumer1 = new AsyncEventingBasicConsumer(_channel);
            consumer1.ReceivedAsync += async (model, ea) => await ReceberMensagemAsync(ea);

            await _channel.BasicConsumeAsync(
                queue: $"gateway-measurements-{GatewayId}",
                autoAck: false,
                consumerTag: $"gateway-measurements-consumer",
                consumer: consumer1
            );

            var consumer2 = new AsyncEventingBasicConsumer(_channel);
            consumer2.ReceivedAsync += async (model, ea) => await ReceberMensagemAsync(ea);

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

    private async Task ReceberMensagemAsync(BasicDeliverEventArgs ea)
    {
        try
        {
            var json = Encoding.UTF8.GetString(ea.Body.ToArray());
            Mensagem? mensagem;
            try
            {
                mensagem = MensagemSerializer.Deserializar(json);
            }
            catch
            {
                mensagem = null;
            }

            if (mensagem != null)
            {
                var fila = ea.ConsumerTag.Contains("control") ? "control" : "measurements";

                _messageQueue.Enqueue(new QueuedMessage
                {
                    Message = mensagem,
                    DeliveryTag = ea.DeliveryTag,
                    Queue = fila
                });

                Log($"Mensagem recebida - Tipo: {mensagem.Tipo}, Sensor: {mensagem.SensorId}, " +
                    $"RoutingKey: {ea.RoutingKey}");
            }
            else
            {
                await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
            }
        }
        catch (Exception ex)
        {
            Log($"Erro ao receber mensagem: {ex.Message}");
            await _channel!.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false);
        }
    }

    public async Task PublicarMensagemControleAsync(string routingKey, Mensagem mensagem)
    {
        try
        {
            if (_channel == null) return;

            var json = JsonSerializer.Serialize(mensagem);
            var body = Encoding.UTF8.GetBytes(json);

            await _channel.BasicPublishAsync(
                exchange: "sensor-control",
                routingKey: routingKey,
                mandatory: false,
                body: body
            );

            Log($"Mensagem de controle publicada: {mensagem.Tipo} ({routingKey})");
        }
        catch (Exception ex)
        {
            Log($"Erro ao publicar mensagem de controle: {ex.Message}");
        }
    }

    private async Task ProcessarFilaAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_messageQueue.TryDequeue(out var queued))
                {
                    try
                    {
                        if (OnMensagemRecebida != null)
                            await OnMensagemRecebida.Invoke(this, queued.Message);

                        await _channel!.BasicAckAsync(queued.DeliveryTag, multiple: false);
                    }
                    catch (Exception ex)
                    {
                        Log($"Erro ao processar mensagem (delivery #{queued.DeliveryTag}): {ex.Message}");

                        try
                        {
                            await _channel!.BasicNackAsync(queued.DeliveryTag, multiple: false, requeue: false);
                        }
                        catch { }
                    }

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
