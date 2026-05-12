using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using SharedProtocol;

namespace Servidor
{
    class Program
    {
        private static Dictionary<string, int> gatewayMap = new Dictionary<string, int>();
        private static Dictionary<int, List<string>> gatewayMessages = new Dictionary<int, List<string>>();
        private static string dataDirectory = "dados";
        private static readonly object logLock = new object();
        private static int gatewayCount = 0;
        private static Mutex gatewayCountMutex = new Mutex();
        private static int portaOriginal;
        private static ServidorMonitor monitor;
        private static AnaliseClient analiseClient = new AnaliseClient();
        private static readonly object gatewayMessagesLock = new object();

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

            monitor = new ServidorMonitor(dataDirectory);

            Thread cmdThread = new Thread(ComandoListener) { IsBackground = true };
            cmdThread.Start();

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

        private static void ComandoListener()
        {
            while (true)
            {
                string? comando = Console.ReadLine();
                if (comando == null) continue;

                comando = comando.Trim().ToLower();

                if (comando == "ajuda" || comando == "help" || comando == "?")
                {
                    ExibirAjuda();
                }
                else if (comando == "sair" || comando == "quit" || comando == "exit")
                {
                    Environment.Exit(0);
                }
                else if (comando.StartsWith("estatisticas") || comando.StartsWith("stats"))
                {
                    var partes = comando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string? tipoDado = partes.Length > 1 ? partes[1] : null;
                    string? sensorId = partes.Length > 2 ? partes[2] : null;
                    _ = ProcessarComandoEstatisticas(tipoDado, sensorId);
                }
                else if (comando.StartsWith("padroes"))
                {
                    var partes = comando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string? tipoDado = partes.Length > 1 ? partes[1] : null;
                    string? sensorId = partes.Length > 2 ? partes[2] : null;
                    _ = ProcessarComandoPadroes(tipoDado, sensorId);
                }
                else if (comando.StartsWith("previsao"))
                {
                    var partes = comando.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    string? tipoDado = partes.Length > 1 ? partes[1] : null;
                    string? sensorId = partes.Length > 2 ? partes[2] : null;
                    _ = ProcessarComandoPrevisao(tipoDado, sensorId);
                }
                else if (comando == "analises" || comando == "list")
                {
                    ListarAnalisesDisponiveis();
                }
                else if (!string.IsNullOrWhiteSpace(comando))
                {
                    Console.WriteLine($"Comando desconhecido: {comando}. Escreva 'ajuda' para ver os comandos disponiveis.");
                }
            }
        }

        private static void ExibirAjuda()
        {
            Console.WriteLine("\n┌──────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│                    COMANDOS DISPONIVEIS                      │");
            Console.WriteLine("├──────────────────────────────────────────────────────────────┤");
            Console.WriteLine("│  ajuda / help / ?     - Mostra esta ajuda                    │");
            Console.WriteLine("│  sair / quit / exit   - Termina o servidor                   │");
            Console.WriteLine("│  analises / list      - Lista tipos de dados disponiveis     │");
            Console.WriteLine("│  stats [tipo] [sensor]- Calcula estatisticas                 │");
            Console.WriteLine("│  padroes [tipo] [sen] - Detecta anomalias e padroes          │");
            Console.WriteLine("│  previsao [tipo][sen] - Preve proximos valores e riscos     │");
            Console.WriteLine("│                                                              │");
            Console.WriteLine("│  Exemplos:                                                   │");
            Console.WriteLine("│    stats temperatura                                         │");
            Console.WriteLine("│    padroes temperatura sensor-01                             │");
            Console.WriteLine("│    previsao humidade                                         │");
            Console.WriteLine("└──────────────────────────────────────────────────────────────┘\n");
        }

        private static async Task ProcessarComandoEstatisticas(string? tipoDado, string? sensorId)
        {
            try
            {
                var tipos = string.IsNullOrWhiteSpace(tipoDado)
                    ? monitor.ObterTiposDados()
                    : new List<string> { tipoDado };

                foreach (var tipo in tipos)
                {
                    var medicoes = monitor.LerMedicoes(tipo);
                    if (medicoes.Count == 0)
                    {
                        Console.WriteLine($"  Sem dados para {tipo}");
                        continue;
                    }

                    var medicoesFiltradas = string.IsNullOrWhiteSpace(sensorId)
                        ? medicoes
                        : medicoes.Where(m => m.sensorId == sensorId).ToList();

                    if (medicoesFiltradas.Count == 0)
                    {
                        Console.WriteLine($"  Sem dados para {tipo} / sensor {sensorId}");
                        continue;
                    }

                    var valores = medicoesFiltradas
                        .Select(m => { double.TryParse(m.valor, out double v); return v; })
                        .ToList();

                    Console.WriteLine($"\n  A analisar {valores.Count} medicoes de {tipo}...");
                    var resultado = await analiseClient.CalcularEstatisticasAsync(
                        sensorId ?? "todos", tipo, valores);

                    if (resultado?.Sucesso == true)
                    {
                        Console.WriteLine($"  ┌─ Estatisticas: {tipo}" + (sensorId != null ? $" (sensor: {sensorId})" : ""));
                        Console.WriteLine($"  ├─ Count:    {resultado.Count}");
                        Console.WriteLine($"  ├─ Media:    {resultado.Media:F2}");
                        Console.WriteLine($"  ├─ Mediana:  {resultado.Mediana:F2}");
                        Console.WriteLine($"  ├─ Desvio:   {resultado.DesvioPadrao:F2}");
                        Console.WriteLine($"  ├─ Variancia:{resultado.Variancia:F2}");
                        Console.WriteLine($"  ├─ Minimo:   {resultado.Minimo:F2}");
                        Console.WriteLine($"  ├─ Maximo:   {resultado.Maximo:F2}");
                        Console.WriteLine($"  ├─ Q1:       {resultado.Q1:F2}");
                        Console.WriteLine($"  └─ Q3:       {resultado.Q3:F2}");
                        Log($"Analise estatistica concluida para {tipo}: media={resultado.Media:F2}, mediana={resultado.Mediana:F2}");
                    }
                    else
                    {
                        Console.WriteLine($"  Erro na analise de {tipo}: {resultado?.Erro ?? "RPC indisponivel"}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Erro ao processar comando: {ex.Message}");
            }
        }

        private static async Task ProcessarComandoPadroes(string? tipoDado, string? sensorId)
        {
            try
            {
                var tipos = string.IsNullOrWhiteSpace(tipoDado)
                    ? monitor.ObterTiposDados()
                    : new List<string> { tipoDado };

                foreach (var tipo in tipos)
                {
                    var medicoes = monitor.LerMedicoes(tipo);
                    if (medicoes.Count == 0) continue;

                    var medicoesFiltradas = string.IsNullOrWhiteSpace(sensorId)
                        ? medicoes
                        : medicoes.Where(m => m.sensorId == sensorId).ToList();

                    if (medicoesFiltradas.Count == 0) continue;

                    var valores = medicoesFiltradas
                        .Select(m => { double.TryParse(m.valor, out double v); return v; })
                        .ToList();

                    Console.WriteLine($"\n  A detetar padroes em {valores.Count} medicoes de {tipo}...");
                    var resultado = await analiseClient.DetetarPadroesAsync(
                        sensorId ?? "todos", tipo, valores);

                    if (resultado?.Sucesso == true)
                    {
                        Console.WriteLine($"  ┌─ Padroes: {tipo}" + (sensorId != null ? $" (sensor: {sensorId})" : ""));
                        Console.WriteLine($"  ├─ Tendencia: {resultado.Tendencia}");
                        Console.WriteLine($"  ├─ Total anomalias: {resultado.TotalAnomalias}");

                        foreach (var anom in resultado.Anomalias.Take(5))
                        {
                            Console.WriteLine($"  ├─ Anomalia #{anom.Indice}: valor={anom.Valor:F2} (z={anom.ZScore:F2}) - {anom.Descricao}");
                        }

                        if (resultado.Anomalias.Count > 5)
                            Console.WriteLine($"  └─ ... e mais {resultado.Anomalias.Count - 5} anomalias");
                        else
                            Console.WriteLine($"  └─ Fim da analise de padroes");

                        Log($"Detecao de padroes concluida para {tipo}: {resultado.TotalAnomalias} anomalias, tendencia {resultado.Tendencia}");
                    }
                    else
                    {
                        Console.WriteLine($"  Erro na detecao de padroes de {tipo}: {resultado?.Erro ?? "RPC indisponivel"}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Erro ao processar comando: {ex.Message}");
            }
        }

        private static async Task ProcessarComandoPrevisao(string? tipoDado, string? sensorId)
        {
            try
            {
                var tipos = string.IsNullOrWhiteSpace(tipoDado)
                    ? monitor.ObterTiposDados()
                    : new List<string> { tipoDado };

                foreach (var tipo in tipos)
                {
                    var medicoes = monitor.LerMedicoes(tipo);
                    if (medicoes.Count == 0) continue;

                    var medicoesFiltradas = string.IsNullOrWhiteSpace(sensorId)
                        ? medicoes
                        : medicoes.Where(m => m.sensorId == sensorId).ToList();

                    if (medicoesFiltradas.Count == 0) continue;

                    var valores = medicoesFiltradas
                        .Select(m => { double.TryParse(m.valor, out double v); return v; })
                        .ToList();

                    Console.WriteLine($"\n  A calcular previsao para {valores.Count} medicoes de {tipo}...");
                    var resultado = await analiseClient.PreverRiscosAsync(
                        sensorId ?? "todos", tipo, valores);

                    if (resultado?.Sucesso == true)
                    {
                        Console.WriteLine($"  ┌─ Previsao: {tipo}" + (sensorId != null ? $" (sensor: {sensorId})" : ""));
                        Console.WriteLine($"  ├─ Proximo valor: {resultado.ProximoValor:F2}");
                        Console.WriteLine($"  ├─ Previsoes: {string.Join(", ", resultado.Previsoes.Select(v => v.ToString("F2")))}");
                        Console.WriteLine($"  ├─ Tendencia: {resultado.Tendencia}");
                        Console.WriteLine($"  └─ Risco: {resultado.Risco}");
                        Log($"Previsao concluida para {tipo}: proximo={resultado.ProximoValor:F2}, tendencia={resultado.Tendencia}, risco={resultado.Risco}");
                    }
                    else
                    {
                        Console.WriteLine($"  Erro na previsao de {tipo}: {resultado?.Erro ?? "RPC indisponivel"}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  Erro ao processar comando: {ex.Message}");
            }
        }

        private static void ListarAnalisesDisponiveis()
        {
            Console.WriteLine("\n┌──────────────────────────────────────────────────────────────┐");
            Console.WriteLine("│              TIPOS DE DADOS DISPONIVEIS                     │");
            Console.WriteLine("├──────────────────────────────────────────────────────────────┤");

            foreach (var tipo in monitor.ObterTiposDados())
            {
                var medicoes = monitor.LerMedicoes(tipo);
                Console.WriteLine($"│  {tipo,-25} {medicoes.Count,5} medicoes");
            }

            Console.WriteLine("└──────────────────────────────────────────────────────────────┘\n");
        }

        private static void ExibirBannerInicial(int porta)
        {
            Console.WriteLine("\n+---------------------------------------------------------------+");
            Console.WriteLine("|        SERVIDOR - Sistema IoT Distribuido - TP2             |");
            Console.WriteLine("|        RPC de Analise e Previsao (FASE 1)                   |");
            Console.WriteLine("+---------------------------------------------------------------+\n");

            Console.WriteLine("Configuracao do Servidor:");
            Console.WriteLine($"  Porto de escuta: {porta}");
            Console.WriteLine($"  Protocolo: TCP/IPv4");
            Console.WriteLine($"  Modo: Multi-threaded com suporte a multiplas gateways");
            Console.WriteLine($"  RPC Analise: http://127.0.0.1:6001");
            Console.WriteLine();
        }

        private static void ExibirServidorPronto(int porta)
        {
            Console.WriteLine("SERVIDOR INICIADO COM SUCESSO!");
            Console.WriteLine("===============================================================");
            Console.WriteLine($"Escutando para conexoes na porta {porta}...");
            Console.WriteLine($"Persistencia de dados: ATIVA (diretorio 'dados/')");
            Console.WriteLine($"Thread-safety: ATIVA (Mutexes por tipo de dado)");
            Console.WriteLine($"RPC Analise: ATIVO (http://127.0.0.1:6001)");
            Console.WriteLine("===============================================================\n");

            Console.WriteLine("Comandos: 'ajuda' para listar comandos disponiveis");
            Console.WriteLine("Exemplo: stats temperatura");
            Console.WriteLine();

            Console.WriteLine("Monitor de Gateways:");
            Console.WriteLine("----------------------------------------------------------------\n");
        }

        private static void HandleGateway(TcpClient client, int gatewayNumber)
        {
            NetworkStream stream = client.GetStream();
            var reader = new StreamReader(stream, new UTF8Encoding(false));
            var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };

            string? gatewayId = null;

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

                    if (msg.Tipo == TiposMensagem.DATA)
                        ProcessarDATA(msg, writer);
                    else
                    {
                        var error = new Mensagem(
                            TiposMensagem.ERROR,
                            msg.SensorId ?? "unknown",
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

            // Persistir dados no ficheiro
            monitor.PersistirMedicao(tipoDado, timestamp, sensorId, valor);

            string gatewayId = msg.Payload.TryGetValue("gateway_id", out var gwObj)
                ? gwObj?.ToString() ?? "desconhecido"
                : "desconhecido";

            int num = gatewayMap.ContainsKey(gatewayId) ? gatewayMap[gatewayId] : -1;

            string linha = $"   [Gateway #{num}] Sensor: {sensorId} | Tipo: {tipoDado} | Valor: {valor} | Hora: {timestamp}";

            lock (gatewayMessagesLock)
            {
                gatewayMessages[num].Add(linha);
            }

            Console.Clear();
            ExibirServidorPronto(portaOriginal);

            lock (gatewayMessagesLock)
            {
                foreach (var kv in gatewayMessages)
                {
                    Console.WriteLine($"Gateway #{kv.Key} conectada!");
                    foreach (var msgLinha in kv.Value)
                        Console.WriteLine(msgLinha);
                    Console.WriteLine();
                }
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
