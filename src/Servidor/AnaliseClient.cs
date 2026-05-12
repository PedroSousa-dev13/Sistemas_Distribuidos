using System.Text;
using System.Text.Json;

namespace Servidor
{
    public class EstatisticasResult
    {
        public bool Sucesso { get; set; }
        public string SensorId { get; set; } = "";
        public string TipoDado { get; set; } = "";
        public int Count { get; set; }
        public double Media { get; set; }
        public double Mediana { get; set; }
        public double DesvioPadrao { get; set; }
        public double Variancia { get; set; }
        public double Minimo { get; set; }
        public double Maximo { get; set; }
        public double Q1 { get; set; }
        public double Q3 { get; set; }
        public string Erro { get; set; } = "";
    }

    public class Anomalia
    {
        public int Indice { get; set; }
        public double Valor { get; set; }
        public double ZScore { get; set; }
        public string Descricao { get; set; } = "";
    }

    public class PadroesResult
    {
        public bool Sucesso { get; set; }
        public string SensorId { get; set; } = "";
        public string TipoDado { get; set; } = "";
        public List<Anomalia> Anomalias { get; set; } = new();
        public int TotalAnomalias { get; set; }
        public string Tendencia { get; set; } = "";
        public string Erro { get; set; } = "";
    }

    public class PrevisaoResult
    {
        public bool Sucesso { get; set; }
        public string SensorId { get; set; } = "";
        public string TipoDado { get; set; } = "";
        public List<double> Previsoes { get; set; } = new();
        public string Tendencia { get; set; } = "";
        public string Risco { get; set; } = "";
        public double ProximoValor { get; set; }
        public string Erro { get; set; } = "";
    }

    public class AnaliseClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public AnaliseClient(string host = "127.0.0.1", int port = 6001)
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
            _baseUrl = $"http://{host}:{port}";
        }

        public AnaliseClient(HttpClient httpClient, string baseUrl = "http://127.0.0.1:6001")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseUrl = baseUrl;
        }

        public async Task<EstatisticasResult?> CalcularEstatisticasAsync(
            string sensorId, string tipoDado, List<double> valores)
        {
            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["sensor_id"] = sensorId,
                    ["tipo_dado"] = tipoDado,
                    ["valores"] = valores,
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/rpc/estatisticas", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<EstatisticasResult>(responseBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Servidor] Erro ao chamar RPC CalcularEstatisticas: {ex.Message}");
                return null;
            }
        }

        public async Task<PadroesResult?> DetetarPadroesAsync(
            string sensorId, string tipoDado, List<double> valores)
        {
            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["sensor_id"] = sensorId,
                    ["tipo_dado"] = tipoDado,
                    ["valores"] = valores,
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/rpc/padroes", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<PadroesResult>(responseBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Servidor] Erro ao chamar RPC DetetarPadroes: {ex.Message}");
                return null;
            }
        }

        public async Task<PrevisaoResult?> PreverRiscosAsync(
            string sensorId, string tipoDado, List<double> valores)
        {
            try
            {
                var payload = new Dictionary<string, object>
                {
                    ["sensor_id"] = sensorId,
                    ["tipo_dado"] = tipoDado,
                    ["valores"] = valores,
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/rpc/previsao", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<PrevisaoResult>(responseBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Servidor] Erro ao chamar RPC PreverRiscos: {ex.Message}");
                return null;
            }
        }
    }
}
