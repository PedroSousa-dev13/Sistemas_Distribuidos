using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace Servidor
{
    /// <summary>
    /// Classe auxiliar para gerir operações de I/O do servidor usando base de dados SQLite.
    /// Fornece métodos thread-safe para persistência de dados.
    /// </summary>
    public class ServidorMonitor
    {
        private readonly string _dataDirectory;
        private readonly string _connectionString;
        private readonly object _dbLock;
        private readonly object _logLock;

        public ServidorMonitor(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
            _dbLock = new object();
            _logLock = new object();

            EnsureDataDirectory();
            _connectionString = $"Data Source={Path.Combine(_dataDirectory, "sistemas_distribuidos.db")}";
            
            InicializarBancoDados();
        }

        /// <summary>
        /// Garante que o diretório de dados existe.
        /// </summary>
        public void EnsureDataDirectory()
        {
            if (!Directory.Exists(_dataDirectory))
            {
                Directory.CreateDirectory(_dataDirectory);
            }
        }

        /// <summary>
        /// Inicializa a base de dados SQLite e cria as tabelas se não existirem.
        /// </summary>
        private void InicializarBancoDados()
        {
            lock (_dbLock)
            {
                using var connection = new SqliteConnection(_connectionString);
                connection.Open();

                string createMedicoesTable = @"
                    CREATE TABLE IF NOT EXISTS medicoes (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        sensor_id TEXT NOT NULL,
                        tipo_dado TEXT NOT NULL,
                        valor REAL NOT NULL,
                        timestamp TEXT NOT NULL,
                        payload_json TEXT
                    );";

                string addPayloadColumn = @"
                    ALTER TABLE medicoes ADD COLUMN payload_json TEXT;";

                string createAnalisesTable = @"
                    CREATE TABLE IF NOT EXISTS analises (
                        id INTEGER PRIMARY KEY AUTOINCREMENT,
                        sensor_id TEXT NOT NULL,
                        tipo_dado TEXT NOT NULL,
                        tipo_analise TEXT NOT NULL,
                        resultado TEXT NOT NULL,
                        timestamp TEXT NOT NULL
                    );";

                using (var cmd = new SqliteCommand(createMedicoesTable, connection))
                {
                    cmd.ExecuteNonQuery();
                }

                try
                {
                    using var addCmd = new SqliteCommand(addPayloadColumn, connection);
                    addCmd.ExecuteNonQuery();
                }
                catch
                {
                    // Coluna ja existe - ignorar
                }

                using (var cmd = new SqliteCommand(createAnalisesTable, connection))
                {
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Persiste uma medição na base de dados SQLite.
        /// </summary>
        public bool PersistirMedicao(string tipoDado, string timestamp, string sensorId, string valor, string? payloadJson = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(tipoDado) || string.IsNullOrWhiteSpace(valor))
                {
                    return false;
                }

                double valorNumerico = 0;
                if (tipoDado == "imagem")
                {
                    valorNumerico = 0;
                }
                else
                {
                    double.TryParse(valor, System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out valorNumerico);
                }

                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    string insertSql = @"
                        INSERT INTO medicoes (sensor_id, tipo_dado, valor, timestamp, payload_json)
                        VALUES ($sensorId, $tipoDado, $valor, $timestamp, $payloadJson);";

                    using var cmd = new SqliteCommand(insertSql, connection);
                    cmd.Parameters.AddWithValue("$sensorId", sensorId);
                    cmd.Parameters.AddWithValue("$tipoDado", tipoDado);
                    cmd.Parameters.AddWithValue("$valor", valorNumerico);
                    cmd.Parameters.AddWithValue("$timestamp", timestamp);
                    cmd.Parameters.AddWithValue("$payloadJson", (object?)payloadJson ?? DBNull.Value);

                    cmd.ExecuteNonQuery();
                }

                Log($"Medição persistida no SQLite: Sensor={sensorId} | Tipo={tipoDado} | Valor={valor}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Erro ao persistir medição no SQLite: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Persiste o resultado de uma análise estatística ou padrão.
        /// </summary>
        public bool PersistirAnalise(string sensorId, string tipoDado, string tipoAnalise, string resultado)
        {
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    string insertSql = @"
                        INSERT INTO analises (sensor_id, tipo_dado, tipo_analise, resultado, timestamp)
                        VALUES ($sensorId, $tipoDado, $tipoAnalise, $resultado, $timestamp);";

                    using var cmd = new SqliteCommand(insertSql, connection);
                    cmd.Parameters.AddWithValue("$sensorId", sensorId);
                    cmd.Parameters.AddWithValue("$tipoDado", tipoDado);
                    cmd.Parameters.AddWithValue("$tipoAnalise", tipoAnalise);
                    cmd.Parameters.AddWithValue("$resultado", resultado);
                    cmd.Parameters.AddWithValue("$timestamp", DateTime.UtcNow.ToString("o"));

                    cmd.ExecuteNonQuery();
                }

                Log($"Análise persistida no SQLite: Sensor={sensorId} | Tipo={tipoDado} | Analise={tipoAnalise}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"Erro ao persistir análise no SQLite: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Regista uma mensagem no ficheiro de log do servidor.
        /// Thread-safe.
        /// </summary>
        public void Log(string message)
        {
            lock (_logLock)
            {
                try
                {
                    string logPath = Path.Combine(_dataDirectory, "servidor.log");
                    File.AppendAllText(logPath, $"{DateTime.Now:o}: {message}\n");
                }
                catch (Exception)
                {
                    // Ignora erros de log do sistema de ficheiros silenciosamente
                }
            }
        }

        /// <summary>
        /// Lê medições do tipo de dado especificado a partir do SQLite.
        /// </summary>
        public List<(string timestamp, string sensorId, string valor)> LerMedicoes(string tipoDado, int limite = 1000)
        {
            var resultados = new List<(string, string, string)>();

            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    string selectSql = @"
                        SELECT timestamp, sensor_id, valor 
                        FROM medicoes 
                        WHERE tipo_dado = $tipoDado
                        ORDER BY id DESC
                        LIMIT $limite;";

                    using var cmd = new SqliteCommand(selectSql, connection);
                    cmd.Parameters.AddWithValue("$tipoDado", tipoDado);
                    cmd.Parameters.AddWithValue("$limite", limite);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string timestamp = reader.GetString(0);
                        string sensorId = reader.GetString(1);
                        double valor = reader.GetDouble(2);
                        resultados.Add((timestamp, sensorId, valor.ToString(System.Globalization.CultureInfo.InvariantCulture)));
                    }
                }

                // As medições foram lidas em ordem reversa (últimas primeiro),
                // mas a CLI e processos supõem ordem cronológica (antigas primeiro)
                resultados.Reverse();
            }
            catch (Exception ex)
            {
                Log($"Erro ao ler medições do SQLite: {ex.Message}");
            }

            return resultados;
        }

        /// <summary>
        /// Retorna a lista de todos os tipos de dados atualmente presentes na BD.
        /// </summary>
        public List<string> ObterTiposDados()
        {
            var tipos = new List<string>();
            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    string selectSql = "SELECT DISTINCT tipo_dado FROM medicoes;";
                    using var cmd = new SqliteCommand(selectSql, connection);
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        tipos.Add(reader.GetString(0));
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"Erro ao obter tipos de dados do SQLite: {ex.Message}");
            }

            // Se ainda não existirem medições na base de dados, devolve os tipos suportados padrão
            if (tipos.Count == 0)
            {
                tipos.AddRange(TiposDadosSuportados);
            }

            return tipos;
        }

        /// <summary>
        /// Retorna o caminho do diretório de dados.
        /// </summary>
        public string DataDirectory => _dataDirectory;

        /// <summary>
        /// Lê medições incluindo o payload_json para tipos nao numericos (ex: imagem).
        /// </summary>
        public List<(string timestamp, string sensorId, string valor, string? payloadJson)> LerMedicoesComPayload(string tipoDado, int limite = 100)
        {
            var resultados = new List<(string, string, string, string?)>();

            try
            {
                lock (_dbLock)
                {
                    using var connection = new SqliteConnection(_connectionString);
                    connection.Open();

                    string selectSql = @"
                        SELECT timestamp, sensor_id, valor, payload_json
                        FROM medicoes
                        WHERE tipo_dado = $tipoDado
                        ORDER BY id DESC
                        LIMIT $limite;";

                    using var cmd = new SqliteCommand(selectSql, connection);
                    cmd.Parameters.AddWithValue("$tipoDado", tipoDado);
                    cmd.Parameters.AddWithValue("$limite", limite);

                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        string timestamp = reader.GetString(0);
                        string sensorId = reader.GetString(1);
                        double valor = reader.IsDBNull(2) ? 0 : reader.GetDouble(2);
                        string? payloadJson = reader.IsDBNull(3) ? null : reader.GetString(3);
                        resultados.Add((timestamp, sensorId, valor.ToString(System.Globalization.CultureInfo.InvariantCulture), payloadJson));
                    }
                }

                resultados.Reverse();
            }
            catch (Exception ex)
            {
                Log($"Erro ao ler medições com payload do SQLite: {ex.Message}");
            }

            return resultados;
        }

        /// <summary>
        /// Retorna a lista de tipos de dados suportados padrão.
        /// </summary>
        public static string[] TiposDadosSuportados => new[]
        {
            "temperatura", "humidade", "qualidade_ar", "ruido", 
            "pm25", "pm10", "luminosidade", "imagem"
        };
    }
}
