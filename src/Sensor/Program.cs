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
        Console.WriteLine("|            SENSOR - Sistema IoT Distribuido                 |");
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine();

        if (!ValidarArgumentos(args))
        {
            ExibirUso();
            return;
        }

        var gatewayIp = args[0];
        var gatewayPort = int.Parse(args[1]);
        var sensorId = args[2];

        Console.WriteLine($"Gateway: {gatewayIp}:{gatewayPort}");
        Console.WriteLine($"Sensor ID: {sensorId}");
        Console.WriteLine();

        using var sensor = new SensorClient(gatewayIp, gatewayPort, sensorId);
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

        if (string.IsNullOrWhiteSpace(args[2]))
        {
            Console.WriteLine("[ERRO] ID do sensor não pode ser vazio.");
            return false;
        }

        return true;
    }

    private static void ExibirUso()
    {
        Console.WriteLine("Uso: Sensor <IP_GATEWAY> <PORTO_GATEWAY> <SENSOR_ID>");
        Console.WriteLine();
        Console.WriteLine("Exemplo:");
        Console.WriteLine("  Sensor 127.0.0.1 5000 sensor_001");
    }

    private static async Task MenuPrincipalAsync(SensorClient sensor)
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

            foreach (var tipo in SensorClient.TiposDadosSuportados)
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

    private static async Task ProcessarOpcaoAsync(SensorClient sensor, string tipoDado, string descricao)
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
