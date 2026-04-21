using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using SharedProtocol;

namespace Servidor
{
    class Program
    {
        private static Dictionary<string, int> gatewayMap = new Dictionary<string, int>();
        private static Dictionary<int, List<string>> gatewayMessages = new Dictionary<int, List<string>>();
        private static Dictionary<string, Mutex> fileMutexes = new Dictionary<string, Mutex>();
        private static Mutex fileMutexesLock = new Mutex();
        private static string dataDirectory = "dados";
        private static readonly object logLock = new object();
        private static int gatewayCount = 0;
        private static Mutex gatewayCountMutex = new Mutex();
        private static int portaOriginal;

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Uso: Servidor <portoEscuta>");
                return;
            }

            int listenPort = int.Parse(args[0]);
            portaOriginal = listenPort;

            ExibirBannerInicial(listenPort);

            if (!Directory.Exists(dataDirectory))
            {
                Directory.CreateDirectory(dataDirectory);
                Log($"Diretório de dados criado: {dataDirectory}");
                Console.WriteLine($"Diretorio de dados criado em: {dataDirectory}\n");
            }
            else
            {
                Console.WriteLine($"Diretorio de dados encontrado: {dataDirectory}\n");
            }

            InitializeFileMutexes();
            Console.WriteLine("Mutexes para ficheiros inicializados\n");

            TcpListener listener = new TcpListener(IPAddress.Any, listenPort);
            listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            try
            {
                listener.Start();
                Log($"Servidor escutando no porto {listenPort}");
                ExibirServidorPronto(listenPort);
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Erro ao iniciar servidor na porta {listenPort}: {ex.Message}");
                return;
            }

            try
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();

                    gatewayCountMutex.WaitOne();
                    gatewayCount++;
                    int gatewayNumber = gatewayCount;
                    gatewayMessages[gatewayNumber] = new List<string>();
                    Console.WriteLine($"\nGateway #{gatewayNumber} conectada! (Total: {gatewayCount})");
                    gatewayCountMutex.ReleaseMutex();

                    Thread gatewayThread = new Thread(() => HandleGateway(client, gatewayNumber))
                    {
                        IsBackground = true
                    };
                    gatewayThread.Start();
                }
            }
            catch (Exception ex)
            {
                Log($"Erro fatal no servidor: {ex.Message}");
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void ExibirBannerInicial(int porta)
        {
            Console.WriteLine("\n+---------------------------------------------------------------+");
            Console.WriteLine("|        SERVIDOR - Sistema IoT Distribuido - FASE 3          |");
            Console.WriteLine("+---------------------------------------------------------------+\n");

            Console.WriteLine("Configuracao do Servidor:");
            Console.WriteLine($"  Porto de escuta: {porta}");
            Console.WriteLine($"  Protocolo: TCP/IPv4");
            Console.WriteLine($"  Modo: Multi-threaded com suporte a multiplas gateways");
            Console.WriteLine();
        }

        private static void ExibirServidorPronto(int porta)
        {
            Console.WriteLine("SERVIDOR INICIADO COM SUCESSO!");
            Console.WriteLine("===============================================================");
            Console.WriteLine($"Escutando para conexoes na porta {porta}...");
            Console.WriteLine($"Persistencia de dados: ATIVA (diretorio 'dados/')");
            Console.WriteLine($"Thread-safety: ATIVA (Mutexes por tipo de dado)");
            Console.WriteLine($"Tipos de dados suportados: 8 (temperatura, humidade, etc.)");
            Console.WriteLine("===============================================================\n");

            Console.WriteLine("Monitor de Gateways:");
            Console.WriteLine("----------------------------------------------------------------\n");
        }

        private static void InitializeFileMutexes()
        {
            string[] tiposDados = { "temperatura", "humidade", "qualidade_ar", "ruido", "pm25", "pm10", "luminosidade", "imagem" };

            fileMutexesLock.WaitOne();
            foreach (var tipo in tiposDados)
                fileMutexes[tipo] = new Mutex();
            fileMutexesLock.ReleaseMutex();
        }

        private static void HandleGateway(TcpClient client, int gatewayNumber)
        {
            NetworkStream stream = client.GetStream();
            var reader = new StreamReader(stream, new UTF8Encoding(false));
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            string? gatewayId = null;
            int mensagensRecebidas = 0;

            try
            {
                while (true)
                {
                    string? line = reader.ReadLine();
                    if (line == null)
                    {
                        Console.WriteLine($"[Servidor] Gateway #{gatewayNumber} desconectada (stream fechado).");
                        break;
                    }

                    var msg = MensagemSerializer.Deserializar(line);

                    if (msg.Payload != null && msg.Payload.TryGetValue("gateway_id", out var gwObj))
                        gatewayId = gwObj?.ToString() ?? gatewayId;
                    else
                        gatewayId ??= $"GATEWAY_{DateTime.Now.Ticks}";

                    gatewayCountMutex.WaitOne();
                    if (!gatewayMap.ContainsKey(gatewayId))
                        gatewayMap[gatewayId] = gatewayNumber;
                    gatewayCountMutex.ReleaseMutex();

                    mensagensRecebidas++;

                    if (msg.Tipo == TiposMensagem.DATA)
                        ProcessarDATA(msg, writer);
                    else
                    {
                        var error = new Mensagem(
                            TiposMensagem.ERROR,
                            msg.SensorId,
                            new Dictionary<string, object> { ["error_code"] = CodigosErro.INVALID_FORMAT },
                            DateTime.UtcNow.ToString("o")
                        );
                        writer.WriteLine(MensagemSerializer.Serializar(error));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Servidor] Gateway #{gatewayNumber} desconectada (erro de ligação).");
                Log($"Erro na ligacao com gateway {gatewayId}: {ex.Message}");
            }
            finally
            {
                gatewayCountMutex.WaitOne();
                gatewayCount--;
                gatewayCountMutex.ReleaseMutex();

                Console.WriteLine($"[Servidor] Ligação com a Gateway #{gatewayNumber} terminada.");
                stream?.Dispose();
                client?.Close();
            }
        }


        private static void ProcessarDATA(Mensagem msg, StreamWriter writer)
        {
            if (msg.Payload == null)
                return;

            if (!msg.Payload.TryGetValue("tipo_dado", out var tipoDadoObj) ||
                !msg.Payload.TryGetValue("valor", out var valorObj))
                return;

            string tipoDado = tipoDadoObj.ToString();
            string valor = valorObj.ToString();
            string timestamp = msg.Timestamp;
            string sensorId = msg.SensorId;

            string gatewayId = msg.Payload.TryGetValue("gateway_id", out var gwObj)
                ? gwObj?.ToString() ?? "desconhecido"
                : "desconhecido";

            int num = gatewayMap.ContainsKey(gatewayId) ? gatewayMap[gatewayId] : -1;

            string linha = $"   [Gateway #{num}] Sensor: {sensorId} | Tipo: {tipoDado} | Valor: {valor} | Hora: {timestamp}";
            gatewayMessages[num].Add(linha);

            Console.Clear();
            ExibirServidorPronto(portaOriginal);

            foreach (var kv in gatewayMessages)
            {
                Console.WriteLine($"Gateway #{kv.Key} conectada!");
                foreach (var msgLinha in kv.Value)
                    Console.WriteLine(msgLinha);
                Console.WriteLine();
            }

            var ack = new Mensagem(
                TiposMensagem.DATA_ACK,
                sensorId,
                new Dictionary<string, object>(),
                DateTime.UtcNow.ToString("o")
            );
            writer.WriteLine(MensagemSerializer.Serializar(ack));
        }

        private static void Log(string message)
        {
            lock (logLock)
            {
                string logPath = Path.Combine(dataDirectory, "servidor.log");
                File.AppendAllText(logPath, $"{DateTime.Now:o}: {message}\n");
            }
        }
    }
}
