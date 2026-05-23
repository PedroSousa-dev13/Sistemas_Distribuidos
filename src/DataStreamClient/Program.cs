using System.Net;
using Sensor;
using DataStreamClient;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine("|         DATA STREAM CLIENT - Sistema IoT Distribuido        |");
        Console.WriteLine("|         (RabbitMQ Pub/Sub - FASE 2)                         |");
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine();

        if (!ValidarArgumentos(args))
        {
            ExibirUso();
            return;
        }

        var rabbitMQHost = args[0];
        var rabbitMQPort = int.Parse(args[1]);
        var caminhoEntrada = args[2];
        var caminhoFicheiro = ResolverCaminhoFicheiro(caminhoEntrada) ?? caminhoEntrada;

        Console.WriteLine($"RabbitMQ: {rabbitMQHost}:{rabbitMQPort}");
        Console.WriteLine($"Ficheiro de dados: {caminhoFicheiro}");
        Console.WriteLine();

        try
        {
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

            var dadosPorSensor = leitor.ObterPorSensor();
            var tarefas = new List<Task>();

            foreach (var (sensorId, registos) in dadosPorSensor)
            {
                var tarefa = ProcessarSensorAsync(rabbitMQHost, rabbitMQPort, sensorId, registos);
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

    private static async Task ProcessarSensorAsync(
        string rabbitMQHost,
        int rabbitMQPort,
        string sensorId,
        List<DataStreamReader.StreamRecord> registos)
    {
        using var sensor = new RabbitMQSensorClient(sensorId, rabbitMQHost, rabbitMQPort);
        sensor.OnLog += (s, msg) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {sensorId}: {msg}");

        try
        {
            if (!await sensor.IniciarAsync())
            {
                Console.WriteLine($"[ERRO] {sensorId}: Falha ao iniciar.");
                return;
            }

            Console.WriteLine($"[OK] {sensorId}: Iniciado. Enviando {registos.Count} medições para RabbitMQ...");
            Console.WriteLine();

            DateTime? tempoAnterior = null;

            foreach (var registro in registos)
            {
                if (tempoAnterior.HasValue)
                {
                    var delay = registro.Timestamp - tempoAnterior.Value;
                    if (delay > TimeSpan.Zero && delay < TimeSpan.FromSeconds(60))
                    {
                        await Task.Delay(delay);
                    }
                }

                await sensor.EnviarMedicaoAsync(registro.TipoDado, registro.Valor);

                tempoAnterior = registro.Timestamp;
            }

            Console.WriteLine($"[OK] {sensorId}: Todas as medições enviadas para RabbitMQ.");
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

    private static bool ValidarArgumentos(string[] args)
    {
        if (args.Length < 3)
        {
            Console.WriteLine("[ERRO] Número insuficiente de argumentos.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("[ERRO] Host RabbitMQ inválido.");
            return false;
        }

        if (!int.TryParse(args[1], out int port) || port < 1 || port > 65535)
        {
            Console.WriteLine($"[ERRO] Porto RabbitMQ inválido: '{args[1]}'");
            return false;
        }

        if (ResolverCaminhoFicheiro(args[2]) is null)
        {
            Console.WriteLine($"[ERRO] Ficheiro não encontrado: '{args[2]}'");
            return false;
        }

        return true;
    }

    private static void ExibirUso()
    {
        Console.WriteLine("Uso: DataStreamClient <RABBITMQ_HOST> <RABBITMQ_PORT> <CAMINHO_CSV>");
        Console.WriteLine();
        Console.WriteLine("Argumentos:");
        Console.WriteLine("  RABBITMQ_HOST    - Endereço do RabbitMQ (ex: localhost)");
        Console.WriteLine("  RABBITMQ_PORT    - Porto AMQP do RabbitMQ (ex: 5672)");
        Console.WriteLine("  CAMINHO_CSV      - Caminho do ficheiro CSV de dados (ex: dados/stream_dados.csv)");
        Console.WriteLine();
        Console.WriteLine("Formato CSV:");
        Console.WriteLine("  timestamp,sensor_id,zona,tipo_dado,valor");
        Console.WriteLine("  2026-04-16T08:00:00.000Z,sensor-01,Sala_A,temperatura,22.5");
        Console.WriteLine();
        Console.WriteLine("Exemplos:");
        Console.WriteLine("  DataStreamClient localhost 5672 dados/stream_dados.csv");
        Console.WriteLine("  DataStreamClient 192.168.1.100 5672 dados/stream_dados.csv");
    }

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
            catch { }
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
