using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private static TcpClient serverClient;
        private static NetworkStream serverStream;
        private static StreamReader serverReader;
        private static string serverEndpoint;
        private static string csvPath;
        private static string gatewayId;
        private static PreProcessamentoClient preProcessamentoClient = new PreProcessamentoClient();
        private static RabbitMQGatewayClient rabbitMQClient;

        private static readonly object serverLock = new object();
        private static bool isServerConnected = false;
        private static bool csvDirty = false;

        static async Task Main(string[] args)
        {
            serverEndpoint = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("SERVER_ENDPOINT") ?? "";
            csvPath = args.Length > 1 ? args[1] : Environment.GetEnvironmentVariable("CSV_PATH") ?? "dados/gateway.csv";
            var rabbitMQHost = args.Length > 2 ? args[2] : Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "rabbitmq";
            var rabbitMQPort = args.Length > 3 && int.TryParse(args[3], out int p) ? p
                : int.TryParse(Environment.GetEnvironmentVariable("RABBITMQ_PORT"), out int p2) ? p2 : 5672;

            if (string.IsNullOrEmpty(serverEndpoint))
            {
                Console.WriteLine("Uso: Gateway <serverEndpoint> <caminhoCSV> [RABBITMQ_HOST] [RABBITMQ_PORT]");
                Console.WriteLine("     Ou definir SERVER_ENDPOINT env var");
                return;
            }
            gatewayId = $"gateway-{Guid.NewGuid():N}";

            ExibirBannerInicial(serverEndpoint, csvPath, rabbitMQHost, rabbitMQPort);

            LerCSV();
            await ConnectToServerAsync();

            rabbitMQClient = new RabbitMQGatewayClient(gatewayId, sensors, rabbitMQHost, rabbitMQPort);
            rabbitMQClient.OnLog += (s, msg) => Log($"[RabbitMQ] {msg}");
            rabbitMQClient.OnMensagemRecebida += ProcessarMensagemRecebidaAsync;

            if (!await rabbitMQClient.IniciarAsync())
            {
                Log("[ERRO] Falha ao iniciar cliente RabbitMQ");
                return;
            }

            Thread watchdogThread = new Thread(WatchdogWorker) { IsBackground = true };
            watchdogThread.Start();

            ExibirGatewayPronta(serverEndpoint);
            Log($"Gateway iniciada com sucesso - ID: {gatewayId}");

            Console.WriteLine("\nPressione Ctrl+C para terminar...\n");

            try
            {
                await Task.Delay(Timeout.Infinite);
            }
            catch (OperationCanceledException)
            {
                Log("Encerrando gateway...");
            }
            finally
            {
                await FlushCsvAsync();
                await rabbitMQClient.PararAsync();
            }
        }

        private static async Task ProcessarMensagemRecebidaAsync(object sender, Mensagem msg)
        {
            try
            {
                var sensorId = msg.SensorId;

                Log($"[PROCESSAMENTO] Tipo: {msg.Tipo}, Sensor: {sensorId}");

                switch (msg.Tipo)
                {
                    case TiposMensagem.REGISTER:
                        ProcessarRegister(msg);
                        break;

                    case TiposMensagem.DATA:
                        await ProcessarDataAsync(msg);
                        break;

                    case TiposMensagem.HEARTBEAT:
                        ProcessarHeartbeat(msg);
                        break;

                    default:
                        Log($"[AVISO] Tipo de mensagem desconhecido: {msg.Tipo}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log($"[ERRO] Erro ao processar mensagem: {ex.Message}");
            }
        }

        private static void ProcessarRegister(Mensagem msg)
        {
            string sensorId = msg.SensorId;
            csvMutex.WaitOne();
            try
            {
                if (sensors.ContainsKey(sensorId))
                {
                    sensors[sensorId].Estado = "ativo";
                    sensors[sensorId].LastSync = DateTime.UtcNow;
                    csvDirty = true;
                    Log($"✓ Sensor {sensorId} registado com sucesso");

                    _ = rabbitMQClient.PublicarMensagemControleAsync(
                        "register_ok",
                        Mensagem.CriarRegisterOk(sensorId)
                    );
                }
                else
                {
                    Log($"✗ Sensor {sensorId} não encontrado no CSV");
                }
            }
            finally
            {
                csvMutex.ReleaseMutex();
            }
        }

        private static async Task ProcessarDataAsync(Mensagem msg)
        {
            string sensorId = msg.SensorId;
            string tipoDado = msg.Payload.GetValueOrDefault("tipo_dado", "")?.ToString() ?? "";
            object valorObj = msg.Payload.GetValueOrDefault("valor");

            Log($"[DATA] Sensor: {sensorId}, Tipo: {tipoDado}, Valor: {valorObj}");

            if (!sensors.ContainsKey(sensorId))
            {
                Log($"[AVISO] Sensor {sensorId} nao registado - dados ignorados");
                return;
            }

            if (!string.IsNullOrEmpty(tipoDado) && valorObj != null)
            {
                double valorOriginal = 0;
                if (tipoDado == "imagem")
                {
                    Log($"[IMAGEM] Dado de imagem recebido: {valorObj} - a preservar valor original");
                }
                else
                {
                    try { valorOriginal = Convert.ToDouble(valorObj); }
                    catch { valorOriginal = 0; }
                }

                if (tipoDado != "imagem")
                {
                    var rpcResult = await preProcessamentoClient
                        .UniformizarDadosAsync(sensorId, tipoDado, valorOriginal, msg.Timestamp);

                    if (rpcResult?.Sucesso == true)
                    {
                        msg.Payload["valor"] = rpcResult.ValorUniformizado;
                        msg.Payload["unidade"] = rpcResult.Unidade;
                        Log($"[RPC] Uniformizar: {sensorId}/{tipoDado} {valorOriginal} -> {rpcResult.ValorUniformizado} {rpcResult.Unidade}");

                        var validacao = await preProcessamentoClient
                            .ValidarDadosAsync(sensorId, tipoDado, rpcResult.ValorUniformizado);

                        if (validacao?.Valido == false)
                        {
                            string erros = string.Join("; ", validacao.Erros);
                            Log($"[RPC] Validar: {sensorId}/{tipoDado} - REJEITADO: {erros}");
                            return;
                        }

                        Log($"[RPC] Validar: {sensorId}/{tipoDado} - VALIDO");
                    }
                    else
                    {
                        Log($"[AVISO] RPC Uniformizar falhou: {rpcResult?.Erro ?? "servico indisponivel"} - a usar valor original");
                    }
                }
                else
                {
                    msg.Payload["unidade"] = "metadata";
                    Log($"[IMAGEM] Dado de imagem preservado: {valorObj}");
                }
            }

            msg.Payload["gateway_id"] = gatewayId;

            if (await SendToServerAsync(msg))
            {
                AtualizarLastSync(sensorId);
            }
        }

        private static void ProcessarHeartbeat(Mensagem msg)
        {
            string sensorId = msg.SensorId;
            AtualizarLastSync(sensorId);
            Log($"[HEARTBEAT] Sensor {sensorId} online");
        }

        private static async Task<bool> SendToServerAsync(Mensagem msg)
        {
            lock (serverLock)
            {
                if (serverStream == null || serverReader == null || !isServerConnected)
                {
                    Log($"✗ Stream do servidor não disponível");
                    return false;
                }

                try
                {
                    string json = MensagemSerializer.Serializar(msg) + "\n";
                    byte[] data = Encoding.UTF8.GetBytes(json);
                    serverStream.Write(data, 0, data.Length);
                    serverStream.Flush();

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
                                        Log($"✓ Mensagem DATA de {msg.SensorId} encaminhada com sucesso");
                                        return true;
                                    }
                                    else
                                    {
                                        Log($"⚠ Resposta inesperada do servidor: {ackMsg?.Tipo}");
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Log($"⚠ Erro ao desserializar ACK: {ex.Message}");
                                }
                            }
                        }
                        catch (TimeoutException)
                        {
                            Log($"✗ Timeout ao aguardar ACK do servidor");
                            return false;
                        }
                        finally
                        {
                            serverStream.ReadTimeout = Timeout.Infinite;
                        }
                    }
                    else
                    {
                        Log($"✓ Mensagem {msg.Tipo} de {msg.SensorId} encaminhada ao servidor");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log($"✗ Erro ao enviar para servidor: {ex.Message}");
                    isServerConnected = false;
                    _ = ReconectarAsync();
                }

                return false;
            }
        }

        private static async Task ReconectarAsync()
        {
            var parts = serverEndpoint.Split(':');
            string ip = parts[0];
            int port = int.Parse(parts[1]);
            int tentativas = 0;
            int maxTentativas = 20;

            while (tentativas < maxTentativas)
            {
                tentativas++;
                try
                {
                    Log($"A reconectar ao servidor {ip}:{port} (tentativa {tentativas}/{maxTentativas})...");
                    var novoClient = new TcpClient();
                    novoClient.Connect(ip, port);

                    lock (serverLock)
                    {
                        try { serverStream?.Dispose(); } catch { }
                        try { serverReader?.Dispose(); } catch { }
                        try { serverClient?.Close(); } catch { }

                        serverClient = novoClient;
                        serverStream = serverClient.GetStream();
                        serverReader = new StreamReader(serverStream, new UTF8Encoding(false));
                        isServerConnected = true;
                    }

                    Log($"✓ Reconectado ao servidor! ({ip}:{port})");
                    return;
                }
                catch
                {
                    if (tentativas >= maxTentativas)
                    {
                        Log($"❌ Falha ao reconectar após {maxTentativas} tentativas");
                        return;
                    }
                    await Task.Delay(5000);
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
                        if ((DateTime.UtcNow - sensor.LastSync.ToUniversalTime()).TotalSeconds > 60 && sensor.Estado == "ativo")
                        {
                            sensor.Estado = "manutencao";
                            csvDirty = true;
                            Log($"Sensor {sensor.SensorId} marcado como manutencao por timeout");
                        }
                    }

                    if (csvDirty)
                    {
                        EscreverCSV();
                        csvDirty = false;
                    }
                }
                finally
                {
                    csvMutex.ReleaseMutex();
                }

                if (!isServerConnected)
                {
                    _ = ReconectarAsync();
                }
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
                    Log("📄 CSV criado vazio");
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
                Log($"✓ CSV carregado: {count} sensor(es)");
                foreach (var sensor in sensors.Values)
                {
                    Log($"  └─ {sensor.SensorId} ({sensor.Estado}) - Zona: {sensor.Zona}");
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
                    csvDirty = true;
                }
            }
            finally
            {
                csvMutex.ReleaseMutex();
            }
        }

        private static Task FlushCsvAsync()
        {
            csvMutex.WaitOne();
            try
            {
                if (csvDirty)
                {
                    EscreverCSV();
                    csvDirty = false;
                }
            }
            finally
            {
                csvMutex.ReleaseMutex();
            }
            return Task.CompletedTask;
        }

        private static void EscreverCSV()
        {
            var lines = sensors.Values.Select(s => 
                $"{s.SensorId}|{s.Estado}|{s.Zona}|[{string.Join(",", s.TiposDados)}]|{s.LastSync:o}");
            File.WriteAllLines(csvPath, lines);
        }

        private static async Task ConnectToServerAsync()
        {
            var parts = serverEndpoint.Split(':');
            string ip = parts[0];
            int port = int.Parse(parts[1]);
            int tentativas = 0;
            int maxTentativas = 10;

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
                    Log($"✗ Falha na tentativa {tentativas}/{maxTentativas}: {ex.Message}");

                    if (tentativas >= maxTentativas)
                    {
                        Log($"❌ Falha após {maxTentativas} tentativas");
                        Console.WriteLine($"Nao foi possivel conectar ao servidor apos {maxTentativas} tentativas.\n");
                        isServerConnected = false;
                        return;
                    }

                    if (tentativas % 3 == 0)
                    {
                        Console.WriteLine($"Aguardando 5 segundos...\n");
                    }
                    await Task.Delay(5000);
                }
            }
        }

        private static void ExibirBannerInicial(string servidor, string csv, string host, int port)
        {
            Console.WriteLine("\n+---------------------------------------------------------------+");
            Console.WriteLine("|       GATEWAY - Sistema IoT Distribuido                     |");
            Console.WriteLine("|       RabbitMQ + RPC Pre-Processamento                      |");
            Console.WriteLine("+---------------------------------------------------------------+\n");

            Console.WriteLine("Configuracao Inicial:");
            Console.WriteLine($"  Servidor remoto: {servidor}");
            Console.WriteLine($"  Ficheiro CSV de sensores: {csv}");
            Console.WriteLine($"  RabbitMQ: {host}:{port}");
            Console.WriteLine($"  RPC Pre-Processamento: http://127.0.0.1:5001");
            Console.WriteLine();
        }

        private static void ExibirGatewayPronta(string servidor)
        {
            Console.WriteLine("GATEWAY INICIADA COM SUCESSO!");
            Console.WriteLine("===============================================================");
            Console.WriteLine($"Conectada ao RabbitMQ (Consumer de tópicos sensor.*)");
            Console.WriteLine($"Ligada ao servidor remoto: {servidor}");
            Console.WriteLine("Sistema de heartbeat ativo");
            Console.WriteLine("Monitor de sensores ativo (timeout: 60s)");
            Console.WriteLine("===============================================================\n");
        }

        private static void Log(string message)
        {
            var timestamp = $"{DateTime.Now:o}";
            Console.WriteLine($"[{timestamp}] {message}");
            LogHelper.Write("gateway.log", message);
        }
    }
}
