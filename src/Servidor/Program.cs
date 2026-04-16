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
        private static Dictionary<string, Mutex> fileMutexes = new Dictionary<string, Mutex>();
        private static Mutex fileMutexesLock = new Mutex();
        private static string dataDirectory = "dados";
        private static readonly object logLock = new object();
        private static int gatewayCount = 0;
        private static Mutex gatewayCountMutex = new Mutex();

        static void Main(string[] args)
        {
            if (args.Length < 1)
            {
                Console.WriteLine("Uso: Servidor <portoEscuta>");
                return;
            }

            int listenPort = int.Parse(args[0]);

            ExibirBannerInicial(listenPort);

            // Criar diretório de dados se não existir
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

            // Inicializar mutexes para ficheiros
            InitializeFileMutexes();
            Console.WriteLine("Mutexes para ficheiros inicializados\n");

            // Iniciar listener TCP
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
                Console.WriteLine($"Tenta uma porta diferente ou aguarda alguns minutos antes de reiniciar.");
                return;
            }

            try
            {
                while (true)
                {
                    TcpClient client = listener.AcceptTcpClient();
                    gatewayCountMutex.WaitOne();
                    try
                    {
                        gatewayCount++;
                        Log($"Nova gateway conectada. Total: {gatewayCount}");
                        Console.WriteLine($"\nGateway #{gatewayCount} conectada! (Total: {gatewayCount})");
                    }
                    finally
                    {
                        gatewayCountMutex.ReleaseMutex();
                    }

                    Thread gatewayThread = new Thread(() => HandleGateway(client))
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
            try
            {
                foreach (var tipo in tiposDados)
                {
                    fileMutexes[tipo] = new Mutex();
                }
            }
            finally
            {
                fileMutexesLock.ReleaseMutex();
            }
        }

        private static void HandleGateway(TcpClient client)
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
                    if (line == null) break;

                    try
                    {
                        var msg = MensagemSerializer.Deserializar(line);
                        gatewayId = gatewayId ?? $"GATEWAY_{DateTime.Now.Ticks}";
                        mensagensRecebidas++;

                        switch (msg.Tipo)
                        {
                            case TiposMensagem.DATA:
                                ProcessarDATA(msg, writer);
                                break;

                            default:
                                Log($"⚠️  Tipo de mensagem desconhecido: {msg.Tipo}");
                                var error = new Mensagem(
                                    TiposMensagem.ERROR,
                                    msg.SensorId,
                                    new Dictionary<string, object> { ["error_code"] = CodigosErro.INVALID_FORMAT },
                                    DateTime.UtcNow.ToString("o")
                                );
                                writer.WriteLine(MensagemSerializer.Serializar(error));
                                break;
                        }
                    }
                    catch (JsonException ex)
                    {
                        Log($"Erro ao deserializar mensagem: {ex.Message}");
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                    Log($"Erro na ligacao com gateway {gatewayId}: {ex.Message}");
            }
            finally
            {
                gatewayCountMutex.WaitOne();
                try
                {
                    if (gatewayCount > 0)
                    {
                        gatewayCount--;
                    }
                    Log($"🔌 Gateway desconectada ({gatewayId}) | Mensagens processadas: {mensagensRecebidas} | Total de gateways: {gatewayCount}");
                    if (gatewayCount > 0)
                    {
                        Console.WriteLine($"Gateway desconectada. Ainda ha {gatewayCount} gateway(s) conectada(s)\n");
                    }
                }
                finally
                {
                    gatewayCountMutex.ReleaseMutex();
                }
                stream?.Dispose();
                client?.Close();
            }
        }

        private static void ProcessarDATA(Mensagem msg, StreamWriter writer)
        {
            try
            {
                // Extrair dados do payload
                if (msg.Payload == null)
                {
                    Log($"Payload nulo na mensagem DATA de {msg.SensorId}");
                    var error = new Mensagem(
                        TiposMensagem.ERROR,
                        msg.SensorId,
                        new Dictionary<string, object> { ["error_code"] = CodigosErro.INVALID_FORMAT },
                        DateTime.UtcNow.ToString("o")
                    );
                    writer.WriteLine(MensagemSerializer.Serializar(error));
                    return;
                }

                // Extrair tipo_dado e valor do payload
                if (!msg.Payload.TryGetValue("tipo_dado", out var tipoDadoObj) ||
                    !msg.Payload.TryGetValue("valor", out var valorObj))
                {
                    Log($"Campos obrigatórios em falta na mensagem DATA de {msg.SensorId}");
                    var error = new Mensagem(
                        TiposMensagem.ERROR,
                        msg.SensorId,
                        new Dictionary<string, object> { ["error_code"] = CodigosErro.INVALID_FORMAT },
                        DateTime.UtcNow.ToString("o")
                    );
                    writer.WriteLine(MensagemSerializer.Serializar(error));
                    return;
                }

                string? tipoDado = tipoDadoObj?.ToString();
                string? valor = valorObj?.ToString();
                string timestamp = msg.Timestamp;
                string sensorId = msg.SensorId;

                if (string.IsNullOrWhiteSpace(tipoDado) || string.IsNullOrWhiteSpace(valor))
                {
                    Log($"Campos tipo_dado/valor inválidos na mensagem DATA de {msg.SensorId}");
                    var error = new Mensagem(
                        TiposMensagem.ERROR,
                        msg.SensorId,
                        new Dictionary<string, object> { ["error_code"] = CodigosErro.INVALID_FORMAT },
                        DateTime.UtcNow.ToString("o")
                    );
                    writer.WriteLine(MensagemSerializer.Serializar(error));
                    return;
                }

                // Persistir medição
                if (PersistirMedicao(tipoDado, timestamp, sensorId, valor))
                {
                    Console.WriteLine($"[DADOS] Sensor: {sensorId} | Tipo: {tipoDado} | Valor: {valor} | Hora: {timestamp}");
                    Log($"[DADOS] Sensor: {sensorId} | Tipo: {tipoDado} | Valor: {valor} | Hora: {timestamp}");

                    // Enviar ACK
                    var ack = new Mensagem(
                        TiposMensagem.DATA_ACK,
                        sensorId,
                        new Dictionary<string, object>(),
                        DateTime.UtcNow.ToString("o")
                    );
                    writer.WriteLine(MensagemSerializer.Serializar(ack));
                }
                else
                {
                    throw new Exception("Falha ao persistir medição");
                }
            }
            catch (Exception ex)
            {
                Log($"❌ Erro ao processar DATA: {ex.Message}");
                var error = new Mensagem(
                    TiposMensagem.ERROR,
                    msg.SensorId,
                    new Dictionary<string, object> { ["error_code"] = "PERSISTENCE_ERROR" },
                    DateTime.UtcNow.ToString("o")
                );
                writer.WriteLine(MensagemSerializer.Serializar(error));
            }
        }

        private static bool PersistirMedicao(string tipoDado, string timestamp, string sensorId, string valor)
        {
            try
            {
                // Validar tipo de dado
                if (!fileMutexes.ContainsKey(tipoDado))
                {
                    Log($"Tipo de dado não suportado: {tipoDado}");
                    return false;
                }

                // Obter mutex para este tipo de dado
                fileMutexesLock.WaitOne();
                Mutex mutex;
                try
                {
                    mutex = fileMutexes[tipoDado];
                }
                finally
                {
                    fileMutexesLock.ReleaseMutex();
                }

                // Lock do mutex específico do tipo
                mutex.WaitOne();
                try
                {
                    string filePath = Path.Combine(dataDirectory, $"{tipoDado}.txt");
                    string linha = $"{timestamp}|{sensorId}|{valor}";

                    File.AppendAllText(filePath, linha + "\n");
                    return true;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                Log($"Erro ao persistir medição: {ex.Message}");
                return false;
            }
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
