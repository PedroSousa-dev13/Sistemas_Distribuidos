# Relatório Técnico — Sistema de Monitorização Ambiental Urbana

## 1. Arquitetura do Sistema

O sistema segue uma arquitetura de microsserviços distribuídos com três camadas: **aquisição** (sensores), **processamento** (gateway + microsserviços Python) e **persistência/análise** (servidor + SQLite + dashboard). A comunicação entre componentes é feita via RabbitMQ (AMQP) para troca de mensagens assíncronas entre sensores e gateway, TCP/IP para comunicação síncrona entre gateway e servidor, e HTTP/REST para RPC entre o gateway/servidor e os microsserviços Python.

### 1.1 Componentes e Portos

| Componente | Tecnologia | Função | Porto |
|-----------|-----------|--------|-------|
| Sensor | C# .NET 9 | Publica medições no RabbitMQ | — (APENAS AMQP) |
| DataStreamClient | C# .NET 9 | Stream de dados CSV para RabbitMQ | — (APENAS AMQP) |
| Gateway | C# .NET 9 | Consome RabbitMQ, orquestra RPC de pré-processamento, encaminha ao servidor via TCP | — |
| Servidor | C# .NET 9 | Persiste dados em SQLite, invoca serviços de análise via RPC HTTP | 7000 (TCP) |
| Pre-Processamento | Python 3 | Uniformização e validação de dados (RPC) | 5001 (HTTP) |
| Analise | Python 3 | Estatísticas, deteção de anomalias, previsão (RPC) | 6001 (HTTP) |
| Interface | Python 3 | Dashboard web com API JSON | 8000 (HTTP) |
| RabbitMQ | 3.13 | Message broker AMQP | 5672 (AMQP), 15672 (gestão) |

### 1.2 Fluxo de Dados

```
Sensor ──AMQP──▶ RabbitMQ ──AMQP──▶ Gateway ──TCP──▶ Servidor ──SQLite──▶ [BD]
                                          │                    │
                                     (RPC HTTP)          (RPC HTTP)
                                          ▼                    ▼
                                   Pre-Processamento      Analise ──▶ Interface
                                        (5001)              (6001)       (8000)
```

**Três fases de comunicação:**
1. **Sensor → Gateway** (AMQP): publica em topic exchange `sensor-measurements` com routing key `sensor.{id}.{tipo}`
2. **Gateway → Servidor** (TCP/IP): mensagens serializadas em JSON, com acknowledge (DATA_ACK)
3. **RPC HTTP**: Gateway → Pre-Processamento (`/rpc/uniformizar`, `/rpc/validar`); Servidor → Analise (`/rpc/estatisticas`, `/rpc/padroes`, `/rpc/previsao`)

---

## 2. Protocolo de Comunicação (SharedProtocol)

### 2.1 Mensagem

Todas as mensagens partilham uma estrutura JSON comum definida em `Mensagem.cs`:

```json
{
  "tipo": "DATA",
  "sensor_id": "sensor-01",
  "payload": { "tipo_dado": "temperatura", "valor": 22.5 },
  "timestamp": "2026-01-01T00:00:00.0000000Z"
}
```

**Tipos de mensagem:** REGISTER, REGISTER_OK, REGISTER_ERR, DATA, DATA_ACK, HEARTBEAT, HEARTBEAT_ACK, ERROR.

O construtor valida:
- `tipo` pertence ao conjunto `TiposMensagem.Validos`
- `sensor_id` é obrigatório para DATA, HEARTBEAT, REGISTER
- `timestamp` segue ISO 8601 (8 formatos aceites, com e sem frações de segundo)

### 2.2 Serializador

`MensagemSerializer` usa `System.Text.Json` com `JsonNamingPolicy.CamelCase`. A deserialização reconstrói a mensagem passando pelo construtor com validação, rejeitando JSON malformado ou campos inválidos.

### 2.3 Códigos de Erro

Erros padronizados: `SENSOR_NOT_FOUND`, `SENSOR_INACTIVE`, `SERVER_UNAVAILABLE`, `INVALID_FORMAT`, `INVALID_DATA_TYPE`.

---

## 3. Componentes em Detalhe

### 3.1 Sensor (C#)

**Ficheiros:** `src/Sensor/Program.cs`, `src/Sensor/RabbitMQSensorClient.cs`

O sensor é um cliente interativo de consola que:
1. Lê do CSV `sensores.csv` os tipos de dados permitidos para o seu ID
2. Conecta-se ao RabbitMQ usando `RabbitMQ.Client` 7.2.1
3. Declara as exchanges (`sensor-measurements` topic, `sensor-control` topic)
4. Publica mensagem REGISTER com os tipos suportados
5. Entra num loop de menu onde o utilizador escolhe o tipo de dado a enviar
6. Envia heartbeat a cada 10s para a exchange `sensor-control` com routing key `heartbeat`

**Publicação AMQP:** usa `BasicPublishAsync` com `DeliveryMode.Persistent`. Para cada medição, publica na topic exchange `sensor-measurements` com routing key `sensor.{SensorId}.{tipoDado}`. Se o sensor tiver zona definida, publica também em `zona.{Zona}.{tipoDado}`.

**Exchange `sensor-control`:** declarada como `Topic` (não `Direct`). O Gateway, o Sensor C# e o script de simulação Python usam `Topic`, necessário para que o binding com wildcard `#` funcione corretamente. Qualquer componente que declare como `Direct` causa `precondition_failed` no RabbitMQ.

### 3.2 DataStreamClient (C#)

**Ficheiro:** `src/DataStreamClient/Program.cs`

Lê um CSV com colunas `timestamp,sensor_id,zona,tipo_dado,valor` e reproduz o stream respeitando os intervalos temporais originais. Cria uma instância `RabbitMQSensorClient` por sensor e publica todas as medições em paralelo.

### 3.3 Gateway (C#)

**Ficheiros:** `src/Gateway/Program.cs`, `RabbitMQGatewayClient.cs`, `PreProcessamentoClient.cs`, `SensorInfo.cs`

#### 3.3.1 Inicialização
1. Lê argumentos (ou env vars): `SERVER_ENDPOINT`, `CSV_PATH`, `RABBITMQ_HOST`, `RABBITMQ_PORT`
2. Gera um `gatewayId` único (`gateway-{GUID}`)
3. Carrega o CSV de sensores (colunas: SensorId | Estado | Zona | [TiposDados] | LastSync)
4. Conecta-se ao Servidor via TCP (até 10 tentativas, 5s de intervalo)
5. Inicia o `RabbitMQGatewayClient` (consome RabbitMQ)
6. Inicia watchdog thread (30s) para detetar timeouts de sensores e reconexão TCP

#### 3.3.2 Processamento de Mensagens RabbitMQ

O Gateway declara filas exclusivas:
- `gateway-measurements-{GatewayId}`: bind à exchange `sensor-measurements` com `sensor.*.#` e `zona.*.#`
- `gateway-control-{GatewayId}`: bind à exchange `sensor-control` com `#`

As mensagens são recebidas por `AsyncEventingBasicConsumer` e colocadas numa `ConcurrentQueue`. Uma task `ProcessarFilaAsync` processa as mensagens em série (delay de 10ms entre mensagens) e invoca `OnMensagemRecebida`.

**Processamento por tipo:**
- **REGISTER**: verifica se o sensor existe no CSV. Se existir, marca como ativo e publica `REGISTER_OK`. Se **não existir**, adiciona ao CSV como `"pendente"` (dados ignorados até que o estado seja alterado manualmente para `"ativo"`).
- **DATA**: verifica o estado do sensor no CSV. Se não for `"ativo"`, os dados são ignorados. Caso contrário, normaliza o valor via RPC `/rpc/uniformizar`; valida via RPC `/rpc/validar`; se rejeitado, descarta; caso contrário, encaminha ao Servidor via TCP.
- **HEARTBEAT**: atualiza `LastSync` do sensor no CSV.

#### 3.3.3 Comunicação TCP com Servidor

`SendToServerAsync` serializa a mensagem, escreve no `NetworkStream` com newline (`\n`), e aguarda resposta. Para mensagens DATA, espera um `DATA_ACK` do servidor com timeout de 5s. Se falhar, marca `isServerConnected = false` e inicia `ReconectarAsync`.

#### 3.3.4 Reconexão e Watchdog

`ReconectarAsync`: até 20 tentativas, 5s de intervalo, recria `TcpClient`, `NetworkStream` e `StreamReader` sob `lock (serverLock)`.

`WatchdogWorker`: a cada 30s, percorre sensores; se `LastSync` > 60s e estado "ativo", marca como "manutenção". Se `isServerConnected == false`, tenta reconexão. Usa flag `csvDirty` para só escrever CSV quando há alterações.

#### 3.3.5 RPC com Retry

`PreProcessamentoClient` implementa `ComRetryAsync<T>`: executa a ação, e em caso de exceção, espera 1s × (i+1) antes de retentar (máx 2 retries). Na última tentativa, retorna `null` em vez de propagar a exceção.

### 3.4 Servidor (C#)

**Ficheiros:** `src/Servidor/Program.cs`, `ServidorMonitor.cs`, `AnaliseClient.cs`

#### 3.4.1 Inicialização
1. Lê `LISTEN_PORT` (env var ou argumento, default 7000)
2. Cria `TcpListener` em `IPAddress.Any` com `ReuseAddress`
3. Inicia `ServidorMonitor` (SQLite)
4. Thread de comandos de consola para estatísticas, padrões e previsões

#### 3.4.2 Gestão de Gateways

Cada gateway que conecta recebe um número sequencial (gatewayCount). As mensagens recebidas são processadas numa thread dedicada (`HandleGateway`):

1. Lê linha do `StreamReader` (JSON + newline)
2. Extrai `gateway_id` do payload
3. Se for DATA: chama `ProcessarDATA`, que persiste em SQLite e envia `DATA_ACK`
4. No `finally`: decrementa gatewayCount, limpa o dicionário `gatewayMessages[gatewayNumber]`

**Semântica de leitura:** o servidor usa `StreamReader.ReadLine()`, que bloqueia atéreceber uma linha completa. Quando o gateway fecha a conexão, `ReadLine()` retorna `null` e o loop termina.

#### 3.4.3 Persistência (SQLite)

`ServidorMonitor` gere uma base SQLite (`dados/sistemas_distribuidos.db`) com duas tabelas:

```sql
medicoes (id, sensor_id, tipo_dado, valor REAL, timestamp, payload_json)
analises (id, sensor_id, tipo_dado, tipo_analise, resultado, timestamp)
```

Todas as operações são protegidas por `lock (_dbLock)`, garantindo exclusão mútua entre threads. A coluna `payload_json` é usada para dados não numéricos (e.g., metadados de imagem).

#### 3.4.4 Análise via RPC

O servidor expõe comandos de consola (`stats`, `padroes`, `previsao`) que:
1. Lêem medições do SQLite
2. Filtram por tipo de dado e sensor
3. Enviam para o serviço `Analise` via HTTP POST
4. Persistem o resultado no SQLite (`analises`)

### 3.5 Pre-Processamento (Python)

**Ficheiro:** `src/PreProcessamento/servico.py`

Servidor HTTP que expõe dois endpoints RPC:
- **POST `/rpc/uniformizar`**: converte valores para unidades padrão (Fahrenheit → Celsius, Kelvin → Celsius, fração → percentagem)
- **POST `/rpc/validar`**: valida contra limites definidos por tipo de dado (e.g., temperatura: -50 a 100 °C)

Limites por tipo de dado:

| Tipo | Mínimo | Máximo | Unidade |
|------|--------|--------|---------|
| temperatura | -50 | 100 | celsius |
| humidade | 0 | 100 | percentagem |
| pm25 | 0 | 1000 | ug/m³ |
| pm10 | 0 | 2000 | ug/m³ |
| qualidade_ar | 0 | 500 | AQI |
| ruido | 0 | 200 | dB |
| luminosidade | 0 | 200000 | lux |

### 3.6 Analise (Python)

**Ficheiros:** `src/Analise/analise_estatistica.py`, `detecao_padroes.py`, `servico.py`

Servidor HTTP com três endpoints:
- **POST `/rpc/estatisticas`**: calcula média, mediana, desvio padrão, variância, mínimo, máximo, Q1, Q3
- **POST `/rpc/padroes`**: deteta anomalias via Z-score (limiar 2.0, crítico em 3.0) e calcula tendência (regressão linear)
- **POST `/rpc/previsao`**: prevê próximos 3 valores usando regressão linear + média móvel (janela 5); classifica risco (baixo/médio/alto) e tendência (subindo/descendo/estável)

### 3.7 Interface (Python)

**Ficheiro:** `src/Interface/main.py`

Dashboard web com API REST (`/api/sensores`, `/api/tipos`, `/api/medicoes`, `/api/analises`) que lê diretamente do SQLite e encaminha pedidos de análise ao serviço `Analise`. Serve ficheiros estáticos (HTML/CSS/JS) de `src/Interface/static/`.

**Variáveis de ambiente:**
- `DB_PATH`: caminho absoluto para o ficheiro SQLite. Em Docker: `/app/dados/sistemas_distribuidos.db`. Por omissão: `../../dados/sistemas_distribuidos.db` relativo a `src/Interface/`.
- `ANALISE_RPC_URL`: URL base do serviço de Análise. Em Docker: `http://analise:6001`. Por omissão: `http://127.0.0.1:6001`.
- `INTERFACE_PORT`: porta do servidor HTTP (default 8000).

---

## 4. Resiliência e Tolerância a Falhas

### 4.1 Reconexão TCP (Gateway → Servidor)
- Conexão inicial: até 10 tentativas, 5s de intervalo
- Reconexão pós-falha: até 20 tentativas, 5s de intervalo
- Toda a reconexão é atómica sob `lock (serverLock)`

### 4.2 Watchdog de Sensores
- Thread separada a cada 30s
- Sensores sem heartbeat há >60s: marcados como "manutenção"
- Escrita CSV diferida (flag `csvDirty`) para reduzir I/O

### 4.3 Retry RPC
- Gateway → Pre-Processamento: 2 retries com backoff exponencial (1s, 2s)
- Servidor → Analise: 2 retries com backoff exponencial (1s, 2s)
- Na última tentativa: retorna null (não propaga exceção)

### 4.4 Graceful Shutdown (Python)
- Todos os serviços Python registam handler `SIGTERM` para `server.server_close()`
- O `SIGINT` (Ctrl+C) é tratado exclusivamente por `except KeyboardInterrupt`, que chama `server.server_close()` — o uso simultâneo de `signal.signal(SIGINT)` causava `OSError 10038` no Windows (socket já fechado durante `serve_forever()`)

### 4.5 Payload Limit
- RPCs Python: rejeitam payloads >10 MB (HTTP 413)

### 4.6 Mutexes e Thread Safety
- C#: `Mutex` para CSV, `lock` para streams TCP, `lock` para SQLite
- Gateway: fila concorrente (`ConcurrentQueue<QueuedMessage>`) para mensagens RabbitMQ
- Servidor: `gatewayCountMutex` para contagem atómica, `gatewayMessagesLock` para dicionário

### 4.7 RabbitMQ Resilience
- `AutomaticRecoveryEnabled = true` na factory
- `RequestedHeartbeat = 30s`
- Filas duráveis com `x-delivery-limit = 3`
- Mensagens com `DeliveryMode.Persistent`
- NACK (não re-enc fila) em caso de falha de processamento

---

## 5. Logs

`LogHelper` (`SharedProtocol/LogHelper.cs`) fornece escrita thread-safe com rotação automática a 5 MB. Usado por Gateway (`gateway.log`) e Servidor (`dados/servidor.log`).

---

## 6. Docker

O sistema completo é orquestrado via `docker-compose.yml` com 6 serviços:

```
docker-compose up --build -d
```

| Serviço | Dockerfile | Depende de |
|---------|-----------|-----------|
| rabbitmq | Imagem oficial | — |
| pre-processamento | Dockerfile.preprocessing | — |
| analise | Dockerfile.analysis | — |
| interface | Dockerfile.interface | analise |
| gateway | Dockerfile.gateway | rabbitmq (healthy), servidor, pre-processamento |
| servidor | Dockerfile.servidor | — |

**Volumes:** o diretório `./dados/` é montado em `/app/dados/` nos serviços `gateway`, `servidor` e `interface` — partilha o CSV de sensores, a base de dados SQLite e os logs.

**Healthchecks:**
- RabbitMQ: `rabbitmq-diagnostics -q ping`
- Serviços Python: `urllib.request.urlopen('http://127.0.0.1:<port>/health')`
- Gateway e Servidor: `grep -q <nome> /proc/1/cmdline` (alternativa a `pgrep`, indisponível na imagem `dotnet/runtime:9.0`)

**Variáveis de ambiente por serviço:**

| Serviço | Variáveis |
|---------|-----------|
| gateway | `SERVER_ENDPOINT`, `CSV_PATH`, `RABBITMQ_HOST`, `RABBITMQ_PORT`, `PRE_PROCESSAMENTO_HOST` |
| servidor | `LISTEN_PORT`, `ANALISE_HOST` |
| interface | `DB_PATH`, `ANALISE_RPC_URL`, `INTERFACE_PORT` |
| pre-processamento | `PRE_PROCESSAMENTO_PORT` |
| analise | `ANALISE_PORT` |

Em Docker, a Interface usa `DB_PATH=/app/dados/sistemas_distribuidos.db` e `ANALISE_RPC_URL=http://analise:6001` para localizar a base de dados e o serviço de análise pelos nomes dos contentores.

---

## 7. Testes

### 7.1 Testes C# (xUnit + Moq) — 14 testes
- **Gateway.Tests (6)**: `PreProcessamentoClient` (uniformizar, validar, erros HTTP, timeout)
- **Servidor.Tests (8)**: `ServidorMonitor` (persistência, tipos de dados, leitura)

### 7.2 Testes Python (pytest) — 25 testes
- **test_analise_estatistica.py (20)**: `calcular_estatisticas` (8), `detetar_anomalias` (6), `prever_proximo` (6)
- **test_detecao_padroes.py (5)**: `analisar_padroes` (lista vazia, sem/com anomalias, limiar, estrutura)

### 7.3 Cobertura
- Funções puras Python: cobertura completa de casos de fronteira
- C#: clientes RPC (validação de erros, retry), persistência SQLite
- Não testado: pipeline integrado (requer RabbitMQ + todos os serviços)

---

## 8. Decisões Técnicas

| Decisão | Alternativa | Justificação |
|---------|-----------|-------------|
| RabbitMQ (AMQP) em vez de TCP direto | TCP socket | Desacoplamento, filas persistentes, routing flexível, recovery automático |
| JSON em vez de binário | Protobuf, MessagePack | Simplicidade, debugging, compatibilidade Python/.NET |
| HTTP RPC em vez de gRPC | gRPC | Sem dependências externas Python, stdlib pura |
| `StreamReader.ReadLine()` para TCP | `Length-prefixed` | Simplicidade, newline como delimiter natural para JSON |
| Threads em vez de async no Servidor | async/await | Compatibilidade com `TcpListener.AcceptTcpClient` síncrono (bloqueante) |
| SQLite em vez de ficheiros de texto | Ficheiros .txt | Consultas estruturadas, concorrência, escalabilidade |
| `lock` em vez de `ReaderWriterLockSlim` | RWLS | Carga de escrita é dominante (poucas leituras concorrentes) |
| `Mutex` para CSV (Gateway) | `lock` | Processo único, `Mutex` garante que escritas são visíveis entre threads |
| `sensor-control` como `Topic` | `Direct` | Necessário para binding com wildcard `#` no Gateway. Todos os componentes (Gateway, Sensor C#, script Python) devem usar o mesmo tipo, sob pena de `precondition_failed` |
| Sensores não listados no CSV registam-se como `"pendente"` | Rejeitar | Compromisso entre segurança e flexibilidade: dados são ignorados até aprovação manual, mas o sensor não é bloqueado |
| `except KeyboardInterrupt` em vez de `signal(SIGINT)` | Handler de sinal | Evita `OSError 10038` no Windows: o signal handler fecha o socket enquanto `serve_forever()` ainda o utiliza |
