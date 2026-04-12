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
        private static string serverEndpoint;
        private static string csvPath;
        private static readonly object logLock = new object();
    private static bool isServerConnected = false;
                Console.WriteLine("Uso: Gateway <portoEscuta> <servidorEndpoint> <caminhoCSV>");
                return;
            }

            int listenPort = int.Parse(args[0]);
            serverEndpoint = args[1];
            csvPath = args[2];

            LerCSV();
            ConnectToServer();

            // Start consumer thread
            Thread consumerThread = new Thread(ConsumerWorker);
            consumerThread.Start();

            // Start watchdog thread
            Thread watchdogThread = new Thread(WatchdogWorker);
            watchdogThread.Start();

            // Start TCP listener
            TcpListener listener = new TcpListener(IPAddress.Any, listenPort);
            listener.Start();
            Console.WriteLine($"Gateway escutando no porto {listenPort}");

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();
                Thread sensorThread = new Thread(() => HandleSensor(client));
                sensorThread.Start();
            }
        }

        private static void LerCSV()
        {
            csvMutex.WaitOne();
            try
            {
                if (!File.Exists(csvPath))
                {
                    File.Create(csvPath).Close();
                    Log("CSV criado vazio.");
                    return;
                }

                sensors.Clear();
                foreach (var line in File.ReadAllLines(csvPath))
                {
                    var parts = line.Split(':');
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
                    }
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
            var lines = sensors.Values.Select(s => $"{s.SensorId}:{s.Estado}:{s.Zona}:[{string.Join(",", s.TiposDados)}]:{s.LastSync:o}");
            File.WriteAllLines(csvPath, lines);
        }

        private static void ConnectToServer()
        {
            var parts = serverEndpoint.Split(':');
            string ip = parts[0];
            int port = int.Parse(parts[1]);
        while (true)
        {
            try
            {
                serverClient = new TcpClient();
                serverClient.Connect(ip, port);
                serverStream = serverClient.GetStream();
                isServerConnected = true;
                Log("Conectado ao servidor.");
                break;
            }
            catch (Exception ex)
            {
                Log($"Falha ao conectar ao servidor: {ex.Message}. Tentando novamente em 5 segundos.");
                Thread.Sleep(5000);
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
                // Retry connect if needed
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
            string json = JsonSerializer.Serialize(msg) + "\n";
            byte[] data = Encoding.UTF8.GetBytes(json);
            serverStream.Write(data, 0, data.Length);

            // Wait for ACK
            var reader = new StreamReader(serverStream, Encoding.UTF8);
            string response = reader.ReadLine();
            var ackMsg = JsonSerializer.Deserialize<Mensagem>(response);
            if (ackMsg.Tipo == TiposMensagem.DATA_ACK)
            {
                Log($"Mensagem DATA de {msg.SensorId} encaminhada e ACK recebida.");
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
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            bool registered = false;
            string sensorId = null;
            try
            {
                while (true)
                {
                    string line = reader.ReadLine();
                    if (line == null) break;
                    var msg = JsonSerializer.Deserialize<Mensagem>(line);
                    sensorId = msg.SensorId;
                    switch (msg.Tipo)
                    {
                        case TiposMensagem.REGISTER:
                            csvMutex.WaitOne();
                            string code = CodigosErro.SENSOR_NOT_FOUND;
                            bool exists = sensors.ContainsKey(sensorId);
                            if (exists)
                            {
                                code = sensors[sensorId].Estado == "ativo" ? null : CodigosErro.SENSOR_INACTIVE;
                            }
                            csvMutex.ReleaseMutex();
                            if (code == null)
                            {
                                registered = true;
                                var response = new Mensagem(TiposMensagem.REGISTER_OK, sensorId, new Dictionary<string, object>(), DateTime.Now.ToString("o"));
                                writer.WriteLine(JsonSerializer.Serialize(response));
                                Log($"Sensor {sensorId} registado com sucesso.");
                            }
                            else
                            {
                                var response = new Mensagem(TiposMensagem.REGISTER_ERR, sensorId, new Dictionary<string, object> { ["code"] = code }, DateTime.Now.ToString("o"));
                                writer.WriteLine(JsonSerializer.Serialize(response));
                                Log($"Registo rejeitado para sensor {sensorId}: {code}.");
                            }
                            break;

                        case TiposMensagem.DATA:
                            if (registered)
                            {
                                if (!isServerConnected)
                                {
                                    var error = new Mensagem(TiposMensagem.ERROR, msg.SensorId, new Dictionary<string, object> { ["code"] = CodigosErro.SERVER_UNAVAILABLE }, DateTime.Now.ToString("o"));
                                    writer.WriteLine(JsonSerializer.Serialize(error));
                                    Log($"Servidor indisponível, enviando ERROR ao sensor {msg.SensorId}.");
                                }
                                else
                                {
                                    csvMutex.WaitOne();
                                    bool ativo = sensors.ContainsKey(msg.SensorId) && sensors[msg.SensorId].Estado == "ativo";
                                    csvMutex.ReleaseMutex();
                                    if (ativo)
                                    {
                                        messageQueue.Add(msg);
                                        Log($"Mensagem DATA de {msg.SensorId} adicionada à fila.");
                                    }
                                    else
                                    {
                                        var error = new Mensagem(TiposMensagem.ERROR, msg.SensorId, new Dictionary<string, object> { ["code"] = CodigosErro.SENSOR_INACTIVE }, DateTime.Now.ToString("o"));
                                        writer.WriteLine(JsonSerializer.Serialize(error));
                                    }
                                }
                            }
                            else
                            {
                                var error = new Mensagem(TiposMensagem.ERROR, msg.SensorId, new Dictionary<string, object> { ["code"] = "NOT_REGISTERED" }, DateTime.Now.ToString("o"));
                                writer.WriteLine(JsonSerializer.Serialize(error));
                            }
                            break;

                        case TiposMensagem.HEARTBEAT:
                            AtualizarLastSync(msg.SensorId);
                            var ack = new Mensagem(TiposMensagem.HEARTBEAT_ACK, msg.SensorId, new Dictionary<string, object>(), DateTime.Now.ToString("o"));
                            writer.WriteLine(JsonSerializer.Serialize(ack));
                            Log($"Heartbeat recebido de {msg.SensorId}.");
                            break;

                        default:
                            var err = new Mensagem(TiposMensagem.ERROR, msg.SensorId, new Dictionary<string, object> { ["code"] = CodigosErro.INVALID_FORMAT }, DateTime.Now.ToString("o"));
                            writer.WriteLine(JsonSerializer.Serialize(err));
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Erro na ligação com sensor {sensorId}: {ex.Message}");
            }
            finally
            {
                client.Close();
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