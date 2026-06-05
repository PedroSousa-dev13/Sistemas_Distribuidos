using System.Globalization;

namespace DataStreamClient;

public class DataStreamReader
{
    private readonly string _filePath;
    private readonly List<StreamRecord> _records = new();

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

    private bool ParsearLinha(string linha, out StreamRecord record)
    {
        record = null!;

        var partes = linha.Split(',');

        if (partes.Length < 5)
            return false;

        try
        {
            var timestamp = DateTime.Parse(partes[0].Trim(), CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal);
            var sensorId = partes[1].Trim();
            var zona = partes[2].Trim();
            var tipoDado = partes[3].Trim();
            var valorStr = partes[4].Trim();

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

    public Dictionary<string, List<StreamRecord>> ObterPorSensor()
    {
        return _records.GroupBy(r => r.SensorId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    public (DateTime Inicio, DateTime Fim)? ObterIntervaloTemporal()
    {
        if (_records.Count == 0)
            return null;

        return (_records.First().Timestamp, _records.Last().Timestamp);
    }
}
