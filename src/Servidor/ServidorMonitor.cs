using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Servidor
{
    /// <summary>
    /// Classe auxiliar para gerir operações de I/O do servidor.
    /// Fornece métodos thread-safe para persistência de dados.
    /// </summary>
    public class ServidorMonitor
    {
        private readonly string _dataDirectory;
        private readonly Dictionary<string, Mutex> _fileMutexes;
        private readonly Mutex _fileMutexesLock;
        private readonly object _logLock;

        public ServidorMonitor(string dataDirectory)
        {
            _dataDirectory = dataDirectory;
            _fileMutexes = new Dictionary<string, Mutex>();
            _fileMutexesLock = new Mutex();
            _logLock = new object();

            EnsureDataDirectory();
            InitializeFileMutexes();
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
        /// Inicializa mutexes para cada tipo de dado.
        /// </summary>
        private void InitializeFileMutexes()
        {
            string[] tiposDados = { 
                "temperatura", "humidade", "qualidade_ar", "ruido", 
                "pm25", "pm10", "luminosidade", "imagem" 
            };

            _fileMutexesLock.WaitOne();
            try
            {
                foreach (var tipo in tiposDados)
                {
                    _fileMutexes[tipo] = new Mutex();
                }
            }
            finally
            {
                _fileMutexesLock.ReleaseMutex();
            }
        }

        /// <summary>
        /// Persiste uma medição num ficheiro específico do tipo de dado.
        /// Garante exclusão mútua por tipo de dado.
        /// </summary>
        public bool PersistirMedicao(string tipoDado, string timestamp, string sensorId, string valor)
        {
            try
            {
                // Validar tipo de dado
                if (!_fileMutexes.ContainsKey(tipoDado))
                {
                    Log($"Tipo de dado não suportado: {tipoDado}");
                    return false;
                }

                // Obter mutex para este tipo de dado
                _fileMutexesLock.WaitOne();
                Mutex mutex;
                try
                {
                    mutex = _fileMutexes[tipoDado];
                }
                finally
                {
                    _fileMutexesLock.ReleaseMutex();
                }

                // Lock do mutex espec�fico do tipo
                mutex.WaitOne();
                try
                {
                    string filePath = Path.Combine(_dataDirectory, $"{tipoDado}.txt");
                    string linha = $"{timestamp}|{sensorId}|{valor}";

                    File.AppendAllText(filePath, linha + "\n");
                    return true;
                }
                finally
                {
                    mutex.ReleaseMutex();
                }
            }
            catch (Exception ex)
            {
                Log($"Erro ao persistir medição: {ex.Message}");
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
                string logPath = Path.Combine(_dataDirectory, "servidor.log");
                File.AppendAllText(logPath, $"{DateTime.Now:o}: {message}\n");
            }
        }

        /// <summary>
        /// Retorna o caminho do diretório de dados.
        /// </summary>
        public string DataDirectory => _dataDirectory;

        /// <summary>
        /// Retorna a lista de tipos de dados suportados.
        /// </summary>
        public static string[] TiposDadosSuportados => new[]
        {
            "temperatura", "humidade", "qualidade_ar", "ruido", 
            "pm25", "pm10", "luminosidade", "imagem"
        };
    }
}
