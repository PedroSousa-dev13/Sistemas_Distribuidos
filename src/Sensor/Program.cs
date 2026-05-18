using System.Net;
using SharedProtocol;

namespace Sensor;

class Program
{
    // Dicionário com descrições dos tipos
    private static readonly Dictionary<string, string> TiposDescricao = new()
    {
        ["temperatura"] = "Temperatura",
        ["humidade"] = "Humidade",
        ["qualidade_ar"] = "Qualidade do Ar",
        ["ruido"] = "Ruído",
        ["pm25"] = "PM2.5",
        ["pm10"] = "PM10",
        ["luminosidade"] = "Luminosidade",
        ["imagem"] = "Imagem/Vídeo"
    };

    static async Task Main(string[] args)
    {
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine("|     SENSOR - Sistema IoT Distribuido (com RabbitMQ)        |");
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine();

        if (!ValidarArgumentos(args))
        {
            ExibirUso();
            return;
        }

        var sensorId = args[0];
        var rabbitMQHost = args.Length > 1 ? args[1] : "localhost";
        var rabbitMQPort = args.Length > 2 && int.TryParse(args[2], out int port) ? port : 5672;

        Console.WriteLine($"RabbitMQ Host: {rabbitMQHost}:{rabbitMQPort}");
        Console.WriteLine($"Sensor ID: {sensorId}");
        Console.WriteLine();

        using var sensor = new RabbitMQSensorClient(sensorId, rabbitMQHost, rabbitMQPort);
        sensor.OnLog += (s, msg) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");

        try
        {
            if (!await sensor.IniciarAsync())
            {
                Console.WriteLine("[ERRO] Falha ao iniciar o sensor.");
                return;
            }

            await MenuPrincipalAsync(sensor);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO FATAL] {ex.Message}");
        }
        finally
        {
            await sensor.PararAsync();
        }
    }

    private static bool ValidarArgumentos(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("[ERRO] Número insuficiente de argumentos.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(args[0]))
        {
            Console.WriteLine("[ERRO] ID do sensor não pode ser vazio.");
            return false;
        }

        return true;
    }

    private static void ExibirUso()
    {
        Console.WriteLine("Uso: Sensor <SENSOR_ID> [RABBITMQ_HOST] [RABBITMQ_PORT]");
        Console.WriteLine();
        Console.WriteLine("Exemplo:");
        Console.WriteLine("  Sensor sensor-01");
        Console.WriteLine("  Sensor sensor-01 localhost 5672");
        Console.WriteLine("  Sensor sensor-01 192.168.1.100 5672");
    }

    private static async Task MenuPrincipalAsync(RabbitMQSensorClient sensor)
    {
        Console.WriteLine();
        Console.WriteLine("==========================================================");
        Console.WriteLine("                      MENU PRINCIPAL                      ");
        Console.WriteLine("==========================================================");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Opções disponíveis:");

            // Construir menu dinâmico
            int index = 1;
            var opcoes = new List<(string numero, string tipo, string descricao)>();

            foreach (var tipo in RabbitMQSensorClient.TiposDadosSuportados)
            {
                if (TiposDescricao.TryGetValue(tipo, out var desc))
                {
                    opcoes.Add((index.ToString(), tipo, desc));
                    Console.WriteLine($"  [{index}] Enviar medição de {desc}");
                    index++;
                }
            }

            Console.WriteLine("  [0] Sair");
            Console.WriteLine();
            Console.Write("Escolha uma opção: ");

            var input = Console.ReadLine()?.Trim();

            if (input == "0")
                break;

            var opcao = opcoes.FirstOrDefault(o => o.numero == input);

            if (opcao.numero == null)
            {
                Console.WriteLine("[ERRO] Opção inválida!");
                continue;
            }

            await ProcessarOpcaoAsync(sensor, opcao.tipo, opcao.descricao);
        }
    }

    private static async Task ProcessarOpcaoAsync(RabbitMQSensorClient sensor, string tipoDado, string descricao)
    {
        object valor;

        if (tipoDado == "imagem")
        {
            valor = $"[imagem_simulada_{Guid.NewGuid():N}]";
            Console.WriteLine($"Simulação de imagem: {valor}");
        }
        else
        {
            Console.Write($"Introduza o valor de {descricao}: ");
            var valorInput = Console.ReadLine()?.Trim();

            if (string.IsNullOrWhiteSpace(valorInput))
            {
                Console.WriteLine("[ERRO] Valor não pode ser vazio!");
                return;
            }

            if (double.TryParse(valorInput, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double valorNumerico))
            {
                valor = valorNumerico;
            }
            else
            {
                valor = valorInput;
            }
        }

        var sucesso = await sensor.EnviarMedicaoAsync(tipoDado, valor);

        if (!sucesso)
        {
            Console.WriteLine("[AVISO] Falha ao enviar medição.");
        }
    }
}
