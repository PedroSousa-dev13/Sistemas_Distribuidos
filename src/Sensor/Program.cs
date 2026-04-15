using System.Net;
using SharedProtocol;

namespace Sensor;

/// <summary>
/// Ponto de entrada do cliente Sensor.
/// </summary>
class Program
{
    /// <summary>
    /// Tipos de dados disponíveis no menu.
    /// </summary>
    private static readonly (string numero, string tipo, string descricao)[] TiposMenu = new[]
    {
        ("1", "temperatura", "Temperatura"),
        ("2", "humidade", "Humidade"),
        ("3", "qualidade_ar", "Qualidade do Ar"),
        ("4", "ruido", "Ruído"),
        ("5", "pm25", "PM2.5"),
        ("6", "pm10", "PM10"),
        ("7", "luminosidade", "Luminosidade"),
        ("8", "imagem", "Imagem/Vídeo"),
    };

    static async Task Main(string[] args)
    {
        Console.WriteLine("+--------------------------------------------------------------+");
        Console.WriteLine("|            SENSOR - Sistema IoT Distribuido                 |");
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
        var sensorId = args[2];

        Console.WriteLine($"Gateway: {gatewayIp}:{gatewayPort}");
        Console.WriteLine($"Sensor ID: {sensorId}");
        Console.WriteLine();

        using var sensor = new SensorClient(gatewayIp, gatewayPort, sensorId);
        sensor.OnLog += (s, msg) => Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {msg}");

        try
        {
            // Iniciar (conectar + registrar + heartbeat)
            if (!await sensor.IniciarAsync())
            {
                Console.WriteLine("[ERRO] Falha ao iniciar o sensor.");
                return;
            }

            // Menu principal
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

        if (string.IsNullOrWhiteSpace(args[2]))
        {
            Console.WriteLine("[ERRO] ID do sensor não pode ser vazio.");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Exibe as instruções de uso do programa.
    /// </summary>
    private static void ExibirUso()
    {
        Console.WriteLine("Uso: Sensor <IP_GATEWAY> <PORTO_GATEWAY> <SENSOR_ID>");
        Console.WriteLine();
        Console.WriteLine("Argumentos:");
        Console.WriteLine("  IP_GATEWAY      - Endereço IP da Gateway (ex: 127.0.0.1)");
        Console.WriteLine("  PORTO_GATEWAY   - Porto de escuta da Gateway (ex: 5000)");
        Console.WriteLine("  SENSOR_ID       - Identificador único do sensor (ex: sensor_001)");
        Console.WriteLine();
        Console.WriteLine("Exemplo:");
        Console.WriteLine("  Sensor 127.0.0.1 5000 sensor_001");
    }

    /// <summary>
    /// Apresenta o menu principal e processa as opções do utilizador.
    /// </summary>
    private static async Task MenuPrincipalAsync(SensorClient sensor)
    {
        Console.WriteLine();
        Console.WriteLine("==========================================================");
        Console.WriteLine("                      MENU PRINCIPAL                      ");
        Console.WriteLine("==========================================================");

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Opcoes disponiveis:");
            foreach (var (numero, _, descricao) in TiposMenu)
            {
                Console.WriteLine($"  [{numero}] Enviar medicao de {descricao}");
            }
            Console.WriteLine("  [0] Sair");
            Console.WriteLine();
            Console.Write("Escolha uma opção: ");

            var input = Console.ReadLine()?.Trim();

            if (input == "0")
                break;

            var opcao = TiposMenu.FirstOrDefault(t => t.numero == input);

            if (opcao.numero == null)
            {
                Console.WriteLine("[ERRO] Opção inválida!");
                continue;
            }

            await ProcessarOpcaoAsync(sensor, opcao.tipo, opcao.descricao);
        }
    }

    /// <summary>
    /// Processa a opção selecionada no menu.
    /// </summary>
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

            // Tentar converter para número se possível
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
