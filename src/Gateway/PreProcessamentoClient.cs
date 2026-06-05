using System.Text;
using System.Text.Json;

namespace Gateway
{
    public class PreProcessamentoRpcResult
    {
        public bool Sucesso { get; set; }
        public string SensorId { get; set; } = "";
        public string TipoDado { get; set; } = "";
        public double ValorUniformizado { get; set; }
        public string Timestamp { get; set; } = "";
        public string Unidade { get; set; } = "";
        public string Erro { get; set; } = "";
    }

    public class ValidacaoRpcResult
    {
        public bool Valido { get; set; }
        public List<string> Erros { get; set; } = new();
        public string SensorId { get; set; } = "";
        public string TipoDado { get; set; } = "";
    }

    public class PreProcessamentoClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;

        public PreProcessamentoClient(string host = "127.0.0.1", int port = 5001)
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            host = Environment.GetEnvironmentVariable("PRE_PROCESSAMENTO_HOST") ?? host;
            _baseUrl = $"http://{host}:{port}";
        }

        public PreProcessamentoClient(HttpClient httpClient, string baseUrl = "http://127.0.0.1:5001")
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
                    Console.WriteLine($"[Gateway] RPC falhou (tentativa {i + 1}/{maxRetries}): {ex.Message}");
                    await Task.Delay(1000 * (i + 1));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Gateway] Erro ao chamar RPC: {ex.Message}");
                    return null;
                }
            }
        }

        public async Task<PreProcessamentoRpcResult?> UniformizarDadosAsync(
            string sensorId, string tipoDado, double valor, string timestamp, string formatoOriginal = "JSON")
        {
            return await ComRetryAsync(async () =>
            {
                var payload = new Dictionary<string, object>
                {
                    ["sensor_id"] = sensorId,
                    ["tipo_dado"] = tipoDado,
                    ["valor"] = valor,
                    ["timestamp"] = timestamp,
                    ["formato_original"] = formatoOriginal,
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/rpc/uniformizar", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<PreProcessamentoRpcResult>(responseBody);
            });
        }

        public async Task<ValidacaoRpcResult?> ValidarDadosAsync(
            string sensorId, string tipoDado, double valor)
        {
            return await ComRetryAsync(async () =>
            {
                var payload = new Dictionary<string, object>
                {
                    ["sensor_id"] = sensorId,
                    ["tipo_dado"] = tipoDado,
                    ["valor"] = valor,
                };

                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_baseUrl}/rpc/validar", content);
                var responseBody = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<ValidacaoRpcResult>(responseBody);
            });
        }
    }
}
