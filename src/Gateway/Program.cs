using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using SharedProtocol;

namespace Gateway
{
    class Program
    {
        private sealed class PendingDataMessage
        {
            public PendingDataMessage(Mensagem message)
            {
                Message = message;
                Completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            public Mensagem Message { get; }

            public TaskCompletionSource<bool> Completion { get; }
        }

        private static Dictionary<string, SensorInfo> sensors = new Dictionary<string, SensorInfo>();
        private static Mutex csvMutex = new Mutex();
        private static BlockingCollection<PendingDataMessage> messageQueue = new BlockingCollection<PendingDataMessage>(100);
        private static TcpClient serverClient;
        private static NetworkStream serverStream;
        private static StreamReader serverReader;
        private static string serverEndpoint;
        private static string csvPath;
        private static string gatewayId;

        private static readonly object logLock = new object();
        private static readonly object serverLock = new object();
        private static bool isServerConnected = false;

        static void Main(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Uso: Gateway <portoEscuta> <servidorEndpoint> <caminhoCSV>");
                return;
            }

            int listenPort = int.Parse(args[0]);
            serverEndpoint = args[1];
            csvPath = args[2];
            gatewayId = $"gateway-{listenPort}";


            ExibirBannerInicial(listenPort, serverEndpoint, csvPath);

            LerCSV();
            ConnectToServer();

            // Start consumer thread
            Thread consumerThread = new Thread(ConsumerWorker) { IsBackground = true };
            consumerThread.Start();

            // Start watchdog thread
            Thread watchdogThread = new Thread(WatchdogWorker) { IsBackground = true };
            watchdogThread.Start();

            // Start TCP listener
            TcpListener listener = new TcpListener(IPAddress.Any, listenPort);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            listener.Start();
            
            ExibirGatewayPronta(listenPort);
            Log($"Gateway iniciada com sucesso no porto {listenPort}");

            while (true)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread sensorThread = new Thread(() => HandleSensor(client)) { IsBackground = true };
                    sensorThread.Start();
                }
                catch (Exception ex)
                {
                    Log($"Erro ao aceitar conexão de sensor: {ex.Message}");
                }
            }
        }

        private static void ExibirBannerInicial(int porta, string servidor, string csv)
        {
            Console.WriteLine("\n+---------------------------------------------------------------+");
            Console.WriteLine("|       GATEWAY - Sistema IoT Distribuido - FASE 3            |");
            Console.WriteLine("+---------------------------------------------------------------+\n");
            
            Console.WriteLine("Configuracao Inicial:");
            Console.WriteLine($"  Porto de escuta para Sensores: {porta}");
            Console.WriteLine($"  Servidor remoto: {servidor}");
            Console.WriteLine($"  Ficheiro CSV de sensores: {csv}");
            Console.WriteLine();
        }

        private static void ExibirGatewayPronta(int porta)
        {
            Console.WriteLine("GATEWAY INICIADA COM SUCESSO!");
            Console.WriteLine("===============================================================");
            Console.WriteLine($"Aguardando conexoes de sensores na porta {porta}...");
            Console.WriteLine("Ligada ao servidor remoto");
            Console.WriteLine("Sistema de heartbeat ativo");
            Console.WriteLine("Monitor de sensores ativo (timeout: 60s)");
            Console.WriteLine("===============================================================\n");
        }

        private static void LerCSV()
        {
            csvMutex.WaitOne();
            try
            {
                if (!File.Exists(csvPath))
                {
                    File.Create(csvPath).Close();
                    Log("📄 CSV criado vazio.");
                    return;
                }

                sensors.Clear();
                int count = 0;
                foreach (var line in File.ReadAllLines(csvPath))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    if (line.TrimStart().StartsWith("#")) continue;
                    
                    var parts = line.Split('|');
                    if (parts.Length >= 5)
                    {
                        var tipos = parts[3].Trim('[', ']').Split(',').Select(s => s.Trim()).ToList();
                        sensors[parts[0]] = new SensorInfo
                        {
                            SensorId = parts[0],
                            Estado = parts[1],
                            Zona = parts[2],
                            TiposDados = tipos,
                            LastSync = DateTime.Parse(parts[4])
                        };
                        count++;
                    }
                }
                Log($"✓ CSV carregado com sucesso: {count} sensor(es) encontrado(s)");
                foreach (var sensor in sensors.Values)
                {
                    Log($"  └─ {sensor.SensorId} ({sensor.Estado}) - Zona: {sensor.Zona} - Tipos: {string.Join(", ", sensor.TiposDados)}");
                }
            }
            finally
            {
                csvMutex.ReleaseMutex();
            }
        }

        private static void AtualizarLastSync(string id)
        {
            csvMutex.WaitOne();
            try
            {
                if (sensors.ContainsKey(id))
                {
                    sensors[id].LastSync = DateTime.UtcNow;
                    EscreverCSV();
                }
            }
            finally
            {
                csvMutex.ReleaseMutex();
            }
        }

        private static void AlterarEstado(string id, string estado)
        {
            csvMutex.WaitOne();
            try
            {
                if (sensors.ContainsKey(id))
                {
                    sensors[id].Estado = estado;
                    EscreverCSV();
                    Log($"Estado do sensor {id} alterado para {estado}");
                }
            }
            finally
            {
                csvMutex.ReleaseMutex();
            }
        }

        private static void EscreverCSV()
        {
            var lines = sensors.Values.Select(s => $"{s.SensorId}|{s.Estado}|{s.Zona}|[{string.Join(",", s.TiposDados)}]|{s.LastSync:o}");
            File.WriteAllLines(csvPath, lines);
        }

        private static void ConnectToServer()
        {
            var parts = serverEndpoint.Split(':');
            string ip = parts[0];
            int port = int.Parse(parts[1]);
            int tentativas = 0;
            int maxTentativas = 10;  // Limite máximo de tentativas
            
            while (tentativas < maxTentativas)
            {
                tentativas++;
                try
                {
                    Console.WriteLine($"A conectar ao servidor {ip}:{port} (tentativa {tentativas}/{maxTentativas})...");
                    serverClient = new TcpClient();
                    serverClient.Connect(ip, port);
                    serverStream = serverClient.GetStream();
                    serverReader = new StreamReader(serverStream, new UTF8Encoding(false));
                    isServerConnected = true;
                    Log($"✓ Conectado ao servidor com SUCESSO! ({ip}:{port})");
                    Console.WriteLine($"Conexao com servidor estabelecida!\n");
                    return;
                }
                catch (Exception ex)
                {
                    Log($"✗ Falha na tentativa {tentativas}/{maxTentativas} de conectar ao servidor: {ex.Message}");
                    
                    if (tentativas >= maxTentativas)
                    {
                        Log($"❌ Falha ao conectar ao servidor após {maxTentativas} tentativas. Servidor pode estar indisponível.");
                        Console.WriteLine($"Nao foi possivel conectar ao servidor apos {maxTentativas} tentativas.\n");
                        isServerConnected = false;
                        return;
                    }
                    
                    if (tentativas % 3 == 0)
                    {
                        Console.WriteLine($"Aguardando 5 segundos antes de tentar novamente...\n");
                    }
                    Thread.Sleep(5000);
                }
            }
        }

        private static void ConsumerWorker()
        {
            while (true)
            {
                try
                {
                    var pendingMessage = messageQueue.Take();
                    var success = SendToServer(pendingMessage.Message);
                    pendingMessage.Completion.TrySetResult(success);
                }
                catch (Exception ex)
                {
                    isServerConnected = false;
                    Log($"Erro no consumidor: {ex.Message}");
                    try
                    {
                        if (serverClient == null || !serverClient.Connected)
                        {
                            ConnectToServer();
                        }
                    }
                    catch
                    {
                        Log("Falha ao reconectar ao servidor.");
                    }
                }
            }
        }

        private static bool SendToServer(Mensagem msg)
        {
            lock (serverLock)
            {
                try
                {
                    if (serverStream == null || serverReader == null)
                    {
                        Log($"✗ Stream do servidor não disponível");
                        isServerConnected = false;
                        return false;
                    }
                    msg.Payload["gateway_id"] = gatewayId;


                    string json = MensagemSerializer.Serializar(msg) + "\n";
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    serverStream.Write(data, 0, data.Length);
                    serverStream.Flush();

                    // Apenas aguardar resposta para mensagens DATA (que esperam DATA_ACK)
                    if (msg.Tipo == TiposMensagem.DATA)
                    {
                        serverStream.ReadTimeout = 5000;
                        try
                        {
                            string response = serverReader.ReadLine();
                            
                            if (response != null)
                            {
                                try
                                {
                                    var ackMsg = MensagemSerializer.Deserializar(response);
                                    if (ackMsg?.Tipo == TiposMensagem.DATA_ACK)
                                    {
                                        Log($"✓ Mensagem DATA de {msg.SensorId} encaminhada com sucesso. ACK recebida do servidor.");
                                        return true;
                                    }
                                    else
                                    {
                                        Log($"⚠ Resposta inesperada do servidor: {ackMsg?.Tipo}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"⚠ Erro ao desserializar ACK do servidor: {ex.Message}");
                                }
                            }
                        }
                        catch (TimeoutException)
                        {
                            Log($"✗ Timeout ao aguardar ACK do servidor para DATA");
                            isServerConnected = false;
                            return false;
                        }
                        finally
                        {
                            serverStream.ReadTimeout = Timeout.Infinite;
                        }
                    }
                    else
                    {
                        Log($"✓ Mensagem {msg.Tipo} de {msg.SensorId} encaminhada ao servidor.");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log($"✗ Erro ao enviar mensagem para servidor: {ex.Message}");
                    isServerConnected = false;
                    return false;
                }

                return false;
            }
        }

        private static void WatchdogWorker()
        {
            while (true)
            {
                Thread.Sleep(30000);
                csvMutex.WaitOne();
                try
                {
                    foreach (var sensor in sensors.Values)
                    {
                        if ((DateTime.UtcNow - sensor.LastSync.ToUniversalTime()).TotalSeconds > 60 && sensor.Estado == "ativo")
                        {
                            sensor.Estado = "manutencao";
                            Log($"Sensor {sensor.SensorId} marcado como manutencao por timeout.");
                        }
                    }
                    EscreverCSV();
                }
                finally
                {
                    csvMutex.ReleaseMutex();
                }
            }
        }

        private static void HandleSensor(TcpClient client)
        {
            NetworkStream stream = client.GetStream();
            var reader = new StreamReader(stream, new UTF8Encoding(false));
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            bool registered = false;
            string sensorId = null;
            string zona = "Desconhecida";

            try
            {
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                    {
                        Console.WriteLine($"[Gateway] Sensor {sensorId} desconectado (stream fechado).");
                        break;
                    }

                    Mensagem msg;
                    try
                    {
                        msg = MensagemSerializer.Deserializar(line);
                    }
                    catch
                    {
                        continue;
                    }

                    sensorId = msg.SensorId;

                    switch (msg.Tipo)
                    {
                        case TiposMensagem.REGISTER:
                            csvMutex.WaitOne();
                            bool exists = sensors.ContainsKey(sensorId);

                            if (exists)
                            {
                                zona = sensors[sensorId].Zona;
                                sensors[sensorId].Estado = "ativo";
                                sensors[sensorId].LastSync = DateTime.UtcNow;
                                EscreverCSV();
                            }
                            csvMutex.ReleaseMutex();

                            if (exists)
                            {
                                registered = true;

                                var response = new Mensagem(
                                    TiposMensagem.REGISTER_OK,
                                    sensorId,
                                    new Dictionary<string, object>
                                    {
                                        ["tipos_dados"] = sensors[sensorId].TiposDados,
                                        ["gateway_id"] = gatewayId
                                    },
                                    DateTime.UtcNow.ToString("o")
                                );

                                writer.WriteLine(MensagemSerializer.Serializar(response));
                            }
                            else
                            {
                                var response = new Mensagem(
                                    TiposMensagem.REGISTER_ERR,
                                    sensorId,
                                    new Dictionary<string, object>
                                    {
                                        ["error_code"] = CodigosErro.SENSOR_NOT_FOUND,
                                        ["description"] = "Sensor não encontrado"
                                    },
                                    DateTime.UtcNow.ToString("o")
                                );

                                writer.WriteLine(MensagemSerializer.Serializar(response));
                            }

                            break;

                        case TiposMensagem.DATA:

                            if (!registered)
                            {
                                var error = new Mensagem(
                                    TiposMensagem.ERROR,
                                    sensorId,
                                    new Dictionary<string, object>
                                    {
                                        ["error_code"] = "NOT_REGISTERED",
                                        ["gateway_id"] = gatewayId
                                    },
                                    DateTime.UtcNow.ToString("o")
                                );

                                writer.WriteLine(MensagemSerializer.Serializar(error));
                                break;
                            }

                            msg.Payload["gateway_id"] = gatewayId;

                            var pending = new PendingDataMessage(msg);
                            messageQueue.Add(pending);

                            if (pending.Completion.Task.Wait(TimeSpan.FromSeconds(10)))
                            {
                                var ack = new Mensagem(
                                    TiposMensagem.DATA_ACK,
                                    sensorId,
                                    new Dictionary<string, object>
                                    {
                                        ["gateway_id"] = gatewayId
                                    },
                                    DateTime.UtcNow.ToString("o")
                                );

                                writer.WriteLine(MensagemSerializer.Serializar(ack));
                            }
                            else
                            {
                                var error = new Mensagem(
                                    TiposMensagem.ERROR,
                                    sensorId,
                                    new Dictionary<string, object>
                                    {
                                        ["error_code"] = CodigosErro.SERVER_UNAVAILABLE,
                                        ["gateway_id"] = gatewayId
                                    },
                                    DateTime.UtcNow.ToString("o")
                                );

                                writer.WriteLine(MensagemSerializer.Serializar(error));
                            }

                            break;

                        case TiposMensagem.HEARTBEAT:

                            AtualizarLastSync(sensorId);

                            var hbAck = new Mensagem(
                                TiposMensagem.HEARTBEAT_ACK,
                                sensorId,
                                new Dictionary<string, object>
                                {
                                    ["gateway_id"] = gatewayId
                                },
                                DateTime.UtcNow.ToString("o")
                            );

                            writer.WriteLine(MensagemSerializer.Serializar(hbAck));
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Gateway] Sensor {sensorId} desconectado (erro: {ex.Message}).");
            }
            finally
            {
                // IMPORTANTE: só fecha a ligação com o SENSOR
                stream?.Close();
                client?.Close();
            }
        }



        private static void Log(string message)
        {
            lock (logLock)
            {
                File.AppendAllText("gateway.log", $"{DateTime.Now:o}: {message}\n");
            }
        }
    }
}