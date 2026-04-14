using System;
using System.IO;
using System.Threading;
using Xunit;
using Servidor;

namespace Servidor.Tests
{
    public class ServidorMonitorTests
    {
        private readonly string _testDataDir = Path.Combine(Path.GetTempPath(), $"ServidorTest_{Guid.NewGuid()}");

        public ServidorMonitorTests()
        {
            // Setup
            if (Directory.Exists(_testDataDir))
                Directory.Delete(_testDataDir, true);
        }

        public void Dispose()
        {
            // Cleanup
            if (Directory.Exists(_testDataDir))
                Directory.Delete(_testDataDir, true);
        }

        [Fact]
        public void EnsureDataDirectory_CriasDiretorio_QuandoNaoExiste()
        {
            var monitor = new ServidorMonitor(_testDataDir);
            
            Assert.True(Directory.Exists(_testDataDir));
        }

        [Fact]
        public void PersistirMedicao_CriaFicheiro_ComFormatoCorreto()
        {
            var monitor = new ServidorMonitor(_testDataDir);
            string timestamp = "2024-01-15T10:30:00.000Z";
            string sensorId = "SENSOR_001";
            string valor = "23.5";

            bool resultado = monitor.PersistirMedicao("temperatura", timestamp, sensorId, valor);

            Assert.True(resultado);
            string filePath = Path.Combine(_testDataDir, "temperatura.txt");
            Assert.True(File.Exists(filePath));

            string conteudo = File.ReadAllText(filePath).Trim();
            Assert.Equal($"{timestamp}|{sensorId}|{valor}", conteudo);
        }

        [Fact]
        public void PersistirMedicao_RetornaFalse_ParaTipoDadoInvalido()
        {
            var monitor = new ServidorMonitor(_testDataDir);

            bool resultado = monitor.PersistirMedicao("tipo_invalido", "2024-01-15T10:30:00.000Z", "SENSOR_001", "100");

            Assert.False(resultado);
        }

        [Fact]
        public void PersistirMedicao_AdicionaMultiplasLinhas()
        {
            var monitor = new ServidorMonitor(_testDataDir);

            monitor.PersistirMedicao("temperatura", "2024-01-15T10:30:00.000Z", "SENSOR_001", "23.5");
            monitor.PersistirMedicao("temperatura", "2024-01-15T10:31:00.000Z", "SENSOR_001", "24.0");
            monitor.PersistirMedicao("temperatura", "2024-01-15T10:32:00.000Z", "SENSOR_002", "22.8");

            string filePath = Path.Combine(_testDataDir, "temperatura.txt");
            string[] linhas = File.ReadAllLines(filePath);

            Assert.Equal(3, linhas.Length);
        }

        [Fact]
        public void PersistirMedicao_ThreadSafe_ComMultiplasThreads()
        {
            var monitor = new ServidorMonitor(_testDataDir);
            int numThreads = 10;
            int linhasPorThread = 100;

            Thread[] threads = new Thread[numThreads];
            for (int i = 0; i < numThreads; i++)
            {
                int threadId = i;
                threads[i] = new Thread(() =>
                {
                    for (int j = 0; j < linhasPorThread; j++)
                    {
                        monitor.PersistirMedicao(
                            "temperatura",
                            DateTime.Now.ToString("o"),
                            $"SENSOR_{threadId}",
                            (20 + j).ToString()
                        );
                    }
                });
                threads[i].Start();
            }

            foreach (var thread in threads)
                thread.Join();

            string filePath = Path.Combine(_testDataDir, "temperatura.txt");
            string[] linhas = File.ReadAllLines(filePath);

            // Verificar que todas as linhas foram escritas (10 threads * 100 linhas)
            Assert.Equal(numThreads * linhasPorThread, linhas.Length);
        }

        [Fact]
        public void Log_CriaFicheiro_ComTimestamp()
        {
            var monitor = new ServidorMonitor(_testDataDir);

            monitor.Log("Teste de log");

            string logPath = Path.Combine(_testDataDir, "servidor.log");
            Assert.True(File.Exists(logPath));

            string conteudo = File.ReadAllText(logPath);
            Assert.Contains("Teste de log", conteudo);
            Assert.Contains("T", conteudo); // ISO format contém T
        }

        [Fact]
        public void TiposDadosSuportados_RetornaTodos()
        {
            var tipos = ServidorMonitor.TiposDadosSuportados;

            Assert.Contains("temperatura", tipos);
            Assert.Contains("humidade", tipos);
            Assert.Contains("qualidade_ar", tipos);
            Assert.Contains("ruido", tipos);
            Assert.Contains("pm25", tipos);
            Assert.Contains("pm10", tipos);
            Assert.Contains("luminosidade", tipos);
            Assert.Contains("imagem", tipos);
            Assert.Equal(8, tipos.Length);
        }

        [Fact]
        public void PersistirMedicao_EmParalelo_TiposDiferentes_SemBloqueio()
        {
            var monitor = new ServidorMonitor(_testDataDir);

            Thread t1 = new Thread(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    monitor.PersistirMedicao("temperatura", DateTime.Now.ToString("o"), "SENSOR_001", i.ToString());
                    Thread.Sleep(10);
                }
            });

            Thread t2 = new Thread(() =>
            {
                for (int i = 0; i < 50; i++)
                {
                    monitor.PersistirMedicao("humidade", DateTime.Now.ToString("o"), "SENSOR_001", i.ToString());
                    Thread.Sleep(10);
                }
            });

            long startTime = DateTime.Now.Ticks;
            t1.Start();
            t2.Start();
            t1.Join();
            t2.Join();
            long endTime = DateTime.Now.Ticks;

            // Verificar que as duas threads completaram
            Assert.True(File.Exists(Path.Combine(_testDataDir, "temperatura.txt")));
            Assert.True(File.Exists(Path.Combine(_testDataDir, "humidade.txt")));

            // Ambas os ficheiros devem ter 50 linhas
            string[] tempLinhas = File.ReadAllLines(Path.Combine(_testDataDir, "temperatura.txt"));
            string[] humLinhas = File.ReadAllLines(Path.Combine(_testDataDir, "humidade.txt"));
            Assert.Equal(50, tempLinhas.Length);
            Assert.Equal(50, humLinhas.Length);
        }
    }
}
