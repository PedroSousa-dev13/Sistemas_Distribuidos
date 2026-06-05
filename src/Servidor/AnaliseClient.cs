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
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            host = Environment.GetEnvironmentVariable("ANALISE_HOST") ?? host;
            _baseUrl = $"http://{host}:{port}";
        }

        public AnaliseClient(HttpClient httpClient, string baseUrl = "http://127.0.0.1:6001")
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _baseUrl = baseUrl;
        }

        private static async Task<T?> ComRetryAsync<T>(Func<Task<T?>> action, int maxRetries = 2) where T : class?
        {
            for (int i = 0; ; i++)
            {
                try
                {
                    return await action();
                }
                catch (Exception ex) when (i < maxRetries)
                {
                    Console.WriteLine($"[Servidor] RPC falhou (tentativa {i + 1}/{maxRetries}): {ex.Message}");
                    await Task.Delay(1000 * (i + 1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Servidor] Erro ao chamar RPC: {ex.Message}");
                    return null;
                }
            }
        }

        public async Task<EstatisticasResult?> CalcularEstatisticasAsync(
            string sensorId, string tipoDado, List<double> valores)
        {
            return await ComRetryAsync(async () =>
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
            });
        }

        public async Task<PadroesResult?> DetetarPadroesAsync(
            string sensorId, string tipoDado, List<double> valores)
        {
            return await ComRetryAsync(async () =>
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
            });
        }

        public async Task<PrevisaoResult?> PreverRiscosAsync(
            string sensorId, string tipoDado, List<double> valores)
        {
            return await ComRetryAsync(async () =>
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
            });
        }
    }
}
