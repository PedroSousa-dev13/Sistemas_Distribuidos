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
        private static Dictionary<string, SensorInfo> sensors = new Dictionary<string, SensorInfo>();
        private static Mutex csvMutex = new Mutex();
        private static BlockingCollection<Mensagem> messageQueue = new BlockingCollection<Mensagem>(100);
        private static TcpClient serverClient;
        private static NetworkStream serverStream;
        private static StreamReader serverReader;
        private static string serverEndpoint;
        private static string csvPath;
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
            Console.WriteLine("\n╔═══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║         GATEWAY - Sistema IoT Distribuído - FASE 3              ║");
            Console.WriteLine("╚═══════════════════════════════════════════════════════════════╝\n");
            
            Console.WriteLine("📋 Configuração Inicial:");
            Console.WriteLine($"   • Porto de escuta para Sensores: {porta}");
            Console.WriteLine($"   • Servidor remoto: {servidor}");
            Console.WriteLine($"   • Ficheiro CSV de sensores: {csv}");
            Console.WriteLine();
        }

        private static void ExibirGatewayPronta(int porta)
        {
            Console.WriteLine("✅ GATEWAY INICIADA COM SUCESSO!");
            Console.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.WriteLine($"🔗 Aguardando conexões de sensores na porta {porta}...");
            Console.WriteLine($"📡 Ligada ao servidor remoto");
            Console.WriteLine($"⏱️  Sistema de heartbeat ativo");
            Console.WriteLine($"🔍 Monitor de sensores ativo (timeout: 60s)");
            Console.WriteLine("═══════════════════════════════════════════════════════════════\n");
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
                    sensors[id].LastSync = DateTime.Now;
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
            
            while (true)
            {
                tentativas++;
                try
                {
                    Console.WriteLine($"🔗 A conectar ao servidor {ip}:{port} (tentativa {tentativas})...");
                    serverClient = new TcpClient();
                    serverClient.Connect(ip, port);
                    serverStream = serverClient.GetStream();
                    serverReader = new StreamReader(serverStream, new UTF8Encoding(false));
                    isServerConnected = true;
                    Log($"✓ Conectado ao servidor com SUCESSO! ({ip}:{port})");
                    Console.WriteLine($"✅ Conexão com servidor estabelecida!\n");
                    break;
                }
                catch (Exception ex)
                {
                    Log($"✗ Falha na tentativa {tentativas} de conectar ao servidor: {ex.Message}");
                    if (tentativas % 3 == 0)
                    {
                        Console.WriteLine($"⏳ Aguardando 5 segundos antes de tentar novamente...\n");
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
                    var msg = messageQueue.Take();
                    SendToServer(msg);
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

        private static void SendToServer(Mensagem msg)
        {
            lock (serverLock)
            {
                try
                {
                    if (serverStream == null || serverReader == null)
                    {
                        Log($"✗ Stream do servidor não disponível");
                        isServerConnected = false;
                        return;
                    }

                    string json = MensagemSerializer.Serializar(msg) + "\n";
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    serverStream.Write(data, 0, data.Length);
                    serverStream.Flush();

                    // Tentar ler resposta com timeout
                    serverStream.ReadTimeout = 5000;
                    string response = serverReader.ReadLine();
                    
                    if (response != null)
                    {
                        try
                        {
                            var ackMsg = MensagemSerializer.Deserializar(response);
                            if (ackMsg?.Tipo == TiposMensagem.DATA_ACK)
                            {
                                Log($"✓ Mensagem DATA de {msg.SensorId} encaminhada com sucesso. ACK recebida do servidor.");
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
                    Log($"✗ Timeout ao aguardar ACK do servidor");
                    isServerConnected = false;
                }
                catch (Exception ex)
                {
                    Log($"✗ Erro ao enviar mensagem para servidor: {ex.Message}");
                    isServerConnected = false;
                }
                finally
                {
                    serverStream.ReadTimeout = Timeout.Infinite;
                }
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
                        if ((DateTime.Now - sensor.LastSync).TotalSeconds > 60 && sensor.Estado == "ativo")
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
                    if (line == null) break;
                    
                    Mensagem msg;
                    try
                    {
                        msg = MensagemSerializer.Deserializar(line);
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Erro ao deserializar mensagem: {ex.Message}. Linha: {line}");
                        continue;
                    }
                    
                    sensorId = msg.SensorId;
                    
                    switch (msg.Tipo)
                    {
                        case TiposMensagem.REGISTER:
                            csvMutex.WaitOne();
                            string code = CodigosErro.SENSOR_NOT_FOUND;
                            bool exists = sensors.ContainsKey(sensorId);
                            if (exists)
                            {
                                zona = sensors[sensorId].Zona;
                                code = sensors[sensorId].Estado == "ativo" ? null : CodigosErro.SENSOR_INACTIVE;
                            }
                            csvMutex.ReleaseMutex();
                            
                            if (code == null)
                            {
                                registered = true;
                                var response = new Mensagem(TiposMensagem.REGISTER_OK, sensorId, new Dictionary<string, object>(), DateTime.Now.ToString("o"));
                                writer.WriteLine(MensagemSerializer.Serializar(response));
                                Log($"✅ [REGISTO] Sensor '{sensorId}' registado com SUCESSO! (Zona: {zona})");
                            }
                            else
                            {
                                var response = new Mensagem(TiposMensagem.REGISTER_ERR, sensorId, new Dictionary<string, object> { ["error_code"] = code }, DateTime.Now.ToString("o"));
                                writer.WriteLine(MensagemSerializer.Serializar(response));
                                Log($"❌ [REGISTO REJEITADO] Sensor '{sensorId}': {code}");
                            }
                            break;

                        case TiposMensagem.DATA:
                            {
                                if (registered)
                                {
                                    if (!isServerConnected)
                                    {
                                        var error = new Mensagem(TiposMensagem.ERROR, msg.SensorId,
                                            new Dictionary<string, object> { ["error_code"] = CodigosErro.SERVER_UNAVAILABLE },
                                            DateTime.Now.ToString("o"));
                                        writer.WriteLine(MensagemSerializer.Serializar(error));
                                        Log($"⚠️  [DADOS] Sensor '{msg.SensorId}': Servidor indisponível");
                                    }
                                    else
                                    {
                                        csvMutex.WaitOne();
                                        bool ativo = sensors.ContainsKey(msg.SensorId) && sensors[msg.SensorId].Estado == "ativo";
                                        csvMutex.ReleaseMutex();

                                        if (ativo)
                                        {
                                            messageQueue.Add(msg);
                                            var tipoDado = msg.Payload?.GetValueOrDefault("tipo_dado")?.ToString() ?? "desconhecido";
                                            var valor = msg.Payload?.GetValueOrDefault("valor")?.ToString() ?? "N/A";
                                            Log($"📊 [DADOS] Sensor '{msg.SensorId}' → {tipoDado}: {valor}");

                                            // ✅ CORREÇÃO: enviar ACK ao sensor imediatamente
                                            var dataAck = new Mensagem(TiposMensagem.DATA_ACK, msg.SensorId,
                                                new Dictionary<string, object>(), DateTime.Now.ToString("o"));
                                            writer.WriteLine(MensagemSerializer.Serializar(dataAck));
                                        }
                                        else
                                        {
                                            var error = new Mensagem(TiposMensagem.ERROR, msg.SensorId,
                                                new Dictionary<string, object> { ["error_code"] = CodigosErro.SENSOR_INACTIVE },
                                                DateTime.Now.ToString("o"));
                                            writer.WriteLine(MensagemSerializer.Serializar(error));
                                            Log($"⚠️  [DADOS] Sensor '{msg.SensorId}': Inativo, mensagem rejeitada");
                                        }
                                    }
                                }
                                else
                                {
                                    var error = new Mensagem(TiposMensagem.ERROR, msg.SensorId,
                                        new Dictionary<string, object> { ["error_code"] = "NOT_REGISTERED" },
                                        DateTime.Now.ToString("o"));
                                    writer.WriteLine(MensagemSerializer.Serializar(error));
                                    Log($"❌ [DADOS] Sensor '{msg.SensorId}': Não registado");
                                }
                                break;
                            }
                            

                        case TiposMensagem.HEARTBEAT:
                            { 
                            }
                            AtualizarLastSync(msg.SensorId);
                            var heartbeatAck = new Mensagem(TiposMensagem.HEARTBEAT_ACK, msg.SensorId, new Dictionary<string, object>(), DateTime.Now.ToString("o"));
                            writer.WriteLine(MensagemSerializer.Serializar(heartbeatAck));
                            Log($"💓 [HEARTBEAT] Sensor '{msg.SensorId}' está vivo (Zona: {zona})");
                            break;

                        default:
                            var err = new Mensagem(TiposMensagem.ERROR, msg.SensorId, new Dictionary<string, object> { ["error_code"] = CodigosErro.INVALID_FORMAT }, DateTime.Now.ToString("o"));
                            writer.WriteLine(MensagemSerializer.Serializar(err));
                            Log($"❌ [ERRO] Sensor '{msg.SensorId}': Tipo de mensagem inválido");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"⚠️  Erro na ligação com sensor {sensorId}: {ex.Message}");
            }
            finally
            {
                Log($"🔌 Desconexão do sensor '{sensorId}'");
                try { client.Close(); } catch { }
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