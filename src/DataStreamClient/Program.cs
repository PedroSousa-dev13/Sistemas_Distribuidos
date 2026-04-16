using System.Net;
using Sensor;
using DataStreamClient;

/// <summary>
/// Cliente de Stream de Dados - simula sensores através de dados de ficheiro.
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine("|         DATA STREAM CLIENT - Sistema IoT Distribuido        |");
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine();

        // Validar argumentos
        if (!ValidarArgumentos(args))
        {
            ExibirUso();
            return;
        }

        var gatewayIp = args[0];
        var gatewayPort = int.Parse(args[1]);
        var caminhoEntrada = args[2];
        var caminhoFicheiro = ResolverCaminhoFicheiro(caminhoEntrada) ?? caminhoEntrada;

        Console.WriteLine($"Gateway: {gatewayIp}:{gatewayPort}");
        Console.WriteLine($"Ficheiro de dados: {caminhoFicheiro}");
        Console.WriteLine();

        try
        {
            // Carregar dados do ficheiro
            var leitor = new DataStreamReader(caminhoFicheiro);

            if (!await leitor.CarregarAsync())
            {
                Console.WriteLine("[ERRO] Falha ao carregar dados.");
                return;
            }

            var intervalo = leitor.ObterIntervaloTemporal();
            if (intervalo.HasValue)
            {
                Console.WriteLine($"Período: {intervalo.Value.Inicio:O} a {intervalo.Value.Fim:O}");
                Console.WriteLine();
            }

            Console.WriteLine("Pressione qualquer tecla para começar o streaming de dados...");
            Console.ReadKey(intercept: true);
            Console.WriteLine();

            // Agrupar por sensor e processar
            var dadosPorSensor = leitor.ObterPorSensor();
            var tarefas = new List<Task>();

            foreach (var (sensorId, registos) in dadosPorSensor)
            {
                var tarefa = ProcessarSensorAsync(gatewayIp, gatewayPort, sensorId, registos);
                tarefas.Add(tarefa);
            }

            await Task.WhenAll(tarefas);

            Console.WriteLine();
            Console.WriteLine("[OK] Streaming completo.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO FATAL] {ex.Message}");
        }
    }

    /// <summary>
    /// Processa o stream de dados para um sensor específico.
    /// </summary>
    private static async Task ProcessarSensorAsync(
        string gatewayIp,
        int gatewayPort,
        string sensorId,
        List<DataStreamReader.StreamRecord> registos)
    {
        using var sensor = new SensorClient(gatewayIp, gatewayPort, sensorId);
        sensor.OnLog += (s, msg) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {sensorId}: {msg}");

        try
        {
            // Conectar e registar
            if (!await sensor.IniciarAsync())
            {
                Console.WriteLine($"[ERRO] {sensorId}: Falha ao iniciar.");
                return;
            }

            Console.WriteLine($"[OK] {sensorId}: Iniciado. Enviando {registos.Count} medições...");
            Console.WriteLine();

            // Enviar medições com timing realista
            DateTime? tempoAnterior = null;

            foreach (var registro in registos)
            {
                // Calcular delay relativo entre registos
                if (tempoAnterior.HasValue)
                {
                    var delay = registro.Timestamp - tempoAnterior.Value;
                    if (delay > TimeSpan.Zero && delay < TimeSpan.FromSeconds(60))
                    {
                        await Task.Delay(delay);
                    }
                }

                // Enviar medição
                await sensor.EnviarMedicaoAsync(registro.TipoDado, registro.Valor);

                tempoAnterior = registro.Timestamp;
            }

            Console.WriteLine($"[OK] {sensorId}: Todas as medições enviadas.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] {sensorId}: {ex.Message}");
        }
        finally
        {
            await sensor.PararAsync();
        }
    }

    /// <summary>
    /// Valida os argumentos da linha de comandos.
    /// </summary>
    private static bool ValidarArgumentos(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("[ERRO] Número insuficiente de argumentos.");
            return false;
        }

        if (!IPAddress.TryParse(args[0], out _))
        {
            Console.WriteLine($"[ERRO] IP inválido: '{args[0]}'");
            return false;
        }

        if (!int.TryParse(args[1], out int port) || port < 1 || port > 65535)
        {
            Console.WriteLine($"[ERRO] Porto inválido: '{args[1]}'");
            return false;
        }

        if (ResolverCaminhoFicheiro(args[2]) is null)
        {
            Console.WriteLine($"[ERRO] Ficheiro não encontrado: '{args[2]}'");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Exibe as instruções de uso do programa.
    /// </summary>
    private static void ExibirUso()
    {
        Console.WriteLine("Uso: DataStreamClient <IP_GATEWAY> <PORTO_GATEWAY> <CAMINHO_CSV>");
        Console.WriteLine();
        Console.WriteLine("Argumentos:");
        Console.WriteLine("  IP_GATEWAY       - Endereço IP da Gateway (ex: 127.0.0.1)");
        Console.WriteLine("  PORTO_GATEWAY    - Porto de escuta da Gateway (ex: 5000)");
        Console.WriteLine("  CAMINHO_CSV      - Caminho do ficheiro CSV de dados (ex: dados/stream_dados.csv)");
        Console.WriteLine();
        Console.WriteLine("Formato CSV:");
        Console.WriteLine("  timestamp,sensor_id,zona,tipo_dado,valor");
        Console.WriteLine("  2026-04-16T08:00:00.000Z,sensor-01,Sala_A,temperatura,22.5");
        Console.WriteLine();
        Console.WriteLine("Exemplo:");
        Console.WriteLine("  DataStreamClient 127.0.0.1 5000 dados/stream_dados.csv");
    }

    /// <summary>
    /// Resolve o caminho do CSV tentando diretório atual, pasta do executável
    /// e diretórios ascendentes para suportar execução via VS Code e Visual Studio.
    /// </summary>
    private static string? ResolverCaminhoFicheiro(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            return null;

        if (File.Exists(caminho))
            return Path.GetFullPath(caminho);

        var candidatos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AdicionarSeValido(string baseDir)
        {
            if (string.IsNullOrWhiteSpace(baseDir))
                return;

            try
            {
                candidatos.Add(Path.GetFullPath(Path.Combine(baseDir, caminho)));
            }
            catch
            {
                // Ignorar caminhos inválidos.
            }
        }

        var cwd = Directory.GetCurrentDirectory();
        var exeDir = AppContext.BaseDirectory;

        AdicionarSeValido(cwd);
        AdicionarSeValido(exeDir);

        var dir = new DirectoryInfo(cwd);
        for (int i = 0; i < 8 && dir is not null; i++)
        {
            AdicionarSeValido(dir.FullName);
            dir = dir.Parent;
        }

        dir = new DirectoryInfo(exeDir);
        for (int i = 0; i < 10 && dir is not null; i++)
        {
            AdicionarSeValido(dir.FullName);
            dir = dir.Parent;
        }

        foreach (var candidato in candidatos)
        {
            if (File.Exists(candidato))
                return candidato;
        }

        return null;
    }
}
