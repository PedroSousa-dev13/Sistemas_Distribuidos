using System.Globalization;

namespace DataStreamClient;

/// <summary>
/// Leitor de ficheiro CSV de dados para streaming.
/// Lê e parseia registos estruturados para envio contínuo.
/// </summary>
public class DataStreamReader
{
    private readonly string _filePath;
    private readonly List<StreamRecord> _records = new();

    /// <summary>
    /// Registo individual de dados do stream.
    /// </summary>
    public record StreamRecord(
        DateTime Timestamp,
        string SensorId,
        string Zona,
        string TipoDado,
        object Valor
    );

    public DataStreamReader(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));

        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Ficheiro não encontrado: {filePath}");
    }

    /// <summary>
    /// Carrega todos os registos do ficheiro CSV.
    /// Formato: timestamp,sensor_id,zona,tipo_dado,valor
    /// </summary>
    public async Task<bool> CarregarAsync()
    {
        try
        {
            var linhas = await File.ReadAllLinesAsync(_filePath);

            if (linhas.Length < 2)
            {
                Console.WriteLine("[ERRO] Ficheiro vazio ou sem header.");
                return false;
            }

            // Saltar header
            for (int i = 1; i < linhas.Length; i++)
            {
                var linha = linhas[i].Trim();

                if (string.IsNullOrEmpty(linha))
                    continue;

                if (!ParsearLinha(linha, out var record))
                {
                    Console.WriteLine($"[AVISO] Linha {i + 1} inválida: {linha}");
                    continue;
                }

                _records.Add(record);
            }

            // Ordenar por timestamp
            _records.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));

            Console.WriteLine($"[OK] Carregados {_records.Count} registos.");
            return _records.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERRO] Falha ao carregar ficheiro: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Parseia uma linha do CSV.
    /// </summary>
    private bool ParsearLinha(string linha, out StreamRecord record)
    {
        record = null!;

        var partes = linha.Split(',');

        if (partes.Length < 5)
            return false;

        try
        {
            var timestamp = DateTime.Parse(partes[0].Trim(), CultureInfo.InvariantCulture, 
                System.Globalization.DateTimeStyles.AdjustToUniversal);
            var sensorId = partes[1].Trim();
            var zona = partes[2].Trim();
            var tipoDado = partes[3].Trim();
            var valorStr = partes[4].Trim();

            // Tentar converter valor para número
            object valor;
            if (double.TryParse(valorStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var valorNumerico))
            {
                valor = valorNumerico;
            }
            else
            {
                valor = valorStr;
            }

            record = new StreamRecord(timestamp, sensorId, zona, tipoDado, valor);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Retorna os registos agrupados por sensor.
    /// </summary>
    public Dictionary<string, List<StreamRecord>> ObterPorSensor()
    {
        return _records.GroupBy(r => r.SensorId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>
    /// Retorna o intervalo temporal dos dados.
    /// </summary>
    public (DateTime Inicio, DateTime Fim)? ObterIntervaloTemporal()
    {
        if (_records.Count == 0)
            return null;

        return (_records.First().Timestamp, _records.Last().Timestamp);
    }
}
