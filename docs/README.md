# Sistema de Monitorizacao Ambiental Urbana - Sistemas Distribuidos

Um sistema IoT distribuido em C# .NET + Python que simula uma rede de sensores ambientais, com comunicacao via RabbitMQ e microservicos de pre-processamento e analise.

## Componentes

| Componente | Tecnologia | Porto | Funcao |
|-----------|-----------|-------|--------|
| **Sensor** | C# .NET 9 | - | Publica medicoes no RabbitMQ |
| **DataStreamClient** | C# .NET 9 | - | Stream de dados a partir de CSV |
| **Gateway** | C# .NET 9 | - | Consome RabbitMQ, orquestra RPC, encaminha ao Servidor |
| **Servidor** | C# .NET 9 | 7000 (TCP) | Persiste dados em SQLite, invoca servicos de analise |
| **Pre-Processamento** | Python 3 | 5001 (HTTP) | Uniformizacao e validacao de dados |
| **Analise** | Python 3 | 6001 (HTTP) | Estatisticas, detecao de anomalias, previsao |
| **Interface** | Python 3 | 8000 (HTTP) | Dashboard web |
| **RabbitMQ** | 3.13 | 5672/15672 | Message broker (AMQP) |

## Fluxo de Dados

```
SENSOR ──AMQP──▶ RABBITMQ ──AMQP──▶ GATEWAY ──TCP──▶ SERVIDOR
                  (5672)              │                  │
                                       │ (RPC HTTP)       │ (RPC HTTP)
                                       ▼                  ▼
                                PRE-PROC (5001)     ANALISE (6001)
                                                       │
                                                       ▼
                                                  INTERFACE (8000)
```

## Quick Start

### 1. Tudo em Docker (recomendado)

```bash
docker-compose up --build -d
```

### 2. Execucao local (desenvolvimento)

```bash
# Infraestrutura (Docker)
docker-compose up -d rabbitmq pre-processamento analise interface

# Terminal 1 - Servidor
dotnet run --project src/Servidor/Servidor.csproj -- 7000

# Terminal 2 - Gateway
dotnet run --project src/Gateway/Gateway.csproj -- 127.0.0.1:7000 ./sensores.csv localhost 5672

# Terminal 3 - Sensor
dotnet run --project src/Sensor/Sensor.csproj -- sensor-01
```

## Estrutura do Projeto

```
Sistemas_Distribuidos/
├── src/
│   ├── Sensor/                    # Publica medicoes no RabbitMQ
│   ├── Gateway/                   # Consome RabbitMQ, orquestra RPC
│   │   ├── Program.cs
│   │   ├── RabbitMQGatewayClient.cs
│   │   ├── PreProcessamentoClient.cs
│   │   └── SensorInfo.cs
│   ├── Servidor/                  # Persiste dados, invoca analise
│   │   ├── Program.cs
│   │   ├── ServidorMonitor.cs
│   │   └── AnaliseClient.cs
│   ├── DataStreamClient/          # Stream de CSV para RabbitMQ
│   ├── SharedProtocol/            # Protocolo comum (Mensagem, serializacao)
│   ├── PreProcessamento/          # Servico Python (porta 5001)
│   ├── Analise/                   # Servico Python (porta 6001)
│   │   ├── servico.py
│   │   ├── analise_estatistica.py
│   │   └── detecao_padroes.py
│   └── Interface/                 # Dashboard web (porta 8000)
├── tests/
│   ├── Gateway.Tests/             # 6 testes xUnit + Moq
│   ├── Servidor.Tests/            # 8 testes xUnit + Moq
│   └── Analise.Tests/             # 25 testes pytest
├── dados/                         # SQLite + logs (runtime)
├── docker-compose.yml             # Orquestracao completa (6 servicos)
├── Dockerfile.gateway             # Dockerfile para Gateway (.NET)
├── Dockerfile.servidor            # Dockerfile para Servidor (.NET)
├── Dockerfile.preprocessing       # Dockerfile para Pre-Processamento
├── Dockerfile.analysis            # Dockerfile para Analise
├── Dockerfile.interface           # Dockerfile para Interface
└── docs/README.md                 # Este ficheiro
```

## Testes

```bash
# Testes C# (14 no total)
dotnet test tests/Gateway.Tests
dotnet test tests/Servidor.Tests

# Testes Python (25 no total)
python -m pytest tests/Analise.Tests/ -v
```

## Variaveis de Ambiente

| Variavel | Default | Descricao |
|----------|---------|-----------|
| `SERVER_ENDPOINT` | arg[0] | Endpoint Servidor (host:porta) |
| `CSV_PATH` | dados/gateway.csv | Caminho ficheiro sensores CSV |
| `RABBITMQ_HOST` | rabbitmq | Host RabbitMQ |
| `RABBITMQ_PORT` | 5672 | Porta RabbitMQ |
| `LISTEN_PORT` | 7000 | Porta de escuta do Servidor |
| `PRE_PROCESSAMENTO_HOST` | 127.0.0.1 | Host do servico de pre-processamento |
| `ANALISE_HOST` | 127.0.0.1 | Host do servico de analise |
| `ANALISE_RPC_URL` | http://127.0.0.1:6001 | URL base do RPC de analise |

## Funcionalidades Implementadas

- **RabbitMQ** com topic exchange (`sensor-measurements`) e direct exchange (`sensor-control`)
- **Reconexao automatica** do Gateway ao Servidor (TCP watchdog)
- **Retry com backoff** nas chamadas RPC (2 retries, delay 1s/2s)
- **Healthchecks** HTTP nos servicos Python (`GET /health`)
- **Graceful shutdown** com `SIGTERM`/`SIGINT` nos servicos Python
- **Limite de payload** de 10 MB nos RPCs
- **Logs com rotacao** (LogHelper, max 5 MB)
- **Flag `csvDirty`** no Gateway para reduzir escritas CSV
- **Dictionary cleanup** de gateways desconectados no Servidor
- **Gateway ID** com GUID completo (sem truncatura)
- **Suporte a env vars** em todos os componentes (Docker-friendly)

## Status

| Aspecto | Status |
|---------|--------|
| Build .NET | 0 erros |
| Testes C# (Gateway) | 6/6 passing |
| Testes C# (Servidor) | 8/8 passing |
| Testes Python | 25/25 passing |
| Docker Compose | 6 servicos |

---

## Arquitetura e Decisoes Tecnicas

### 1. Porque Microserviços?

O sistema foi desenhado como **6 microservicos independentes** por varias razoes:

- **Separacao de responsabilidades**: Cada componente tem uma unica funcao — o Sensor apenas publica dados, o Gateway apenas orquestra, o Servidor apenas persiste, etc.
- **Escalabilidade horizontal**: Podemos duplicar Gateways ou Servidores sem alterar outros componentes.
- **Tecnologias mistas**: Os sensores e gateway sao C# .NET 9 (performance, tipagem forte), enquanto pre-processamento, analise e interface sao Python (rapidez de desenvolvimento, bibliotecas cientificas).
- **Isolamento via Docker**: Cada servico corre num container proprio, com rede propria (`sensor-network` bridge), healthchecks independentes e logs separados.
- **Resiliencia**: Se o servico de Analise cair, o Servidor continua a persistir dados — apenas as analises sao adiadas.

### 2. RabbitMQ (AMQP) — Mensagens Assincronas

**Onde e usado:** Sensor --> RabbitMQ --> Gateway

**Protocolo:** AMQP 0-9-1 (Advanced Message Queuing Protocol)

**Conceito fundamental:** Em vez do Sensor comunicar diretamente com o Gateway (acoplamento direto), o Sensor publica mensagens num *broker* intermediario. O Gateway consome essas mensagens quando quer. Isto desacopla completamente o produtor do consumidor.

**Exchanges e Routing:**

| Exchange | Tipo | Funcao | Routing Key |
|----------|------|--------|-------------|
| `sensor-measurements` | Topic | Medicoes de sensores | `sensor.<sensor_id>.<tipo>` ou `zona.<zona>.<tipo>` |
| `sensor-control` | Topic | Registo, heartbeat, controlo | `register`, `heartbeat`, `register_ok` |

O tipo **Topic** permite routing flexivel — o Gateway pode subscrever `sensor.*.#` (todos os sensores) ou `zona.A.*` (apenas zona A).

**Vantagens deste padrao:**
- **Buffering**: Se o Gateway estiver temporariamente indisponivel, as mensagens ficam na queue ate serem processadas.
- **Desacoplamento**: O Sensor nao precisa de saber onde esta o Gateway (nao ha IP/porta hardcoded).
- **Tolerancia a falhas**: O `AutomaticRecoveryEnabled = true` e `RequestedHeartbeat = 30s` garantem reconexao automatica.
- **Entrega garantida**: `DeliveryMode = Persistent` e `autoAck: false` asseguram que mensagens nao se perdem.
- **Fan-out natural**: Multiplos Gateways podem consumir o mesmo exchange.

**Fonte no codigo:**
- `src/Sensor/RabbitMQSensorClient.cs:131-160` — Declaracao de exchanges
- `src/Sensor/RabbitMQSensorClient.cs:180-235` — Publicacao com routing keys
- `src/Gateway/RabbitMQGatewayClient.cs:112-174` — Declaracao de exchanges e queues com binds
- `src/Gateway/RabbitMQGatewayClient.cs:276-318` — Consumidor com fila e ACK manual

### 3. TCP (Socket Direto) — Gateway --> Servidor

**Onde e usado:** Gateway envia dados ao Servidor via ligacao TCP persistente na porta 7000.

**Conceito fundamental:** TCP (Transmission Control Protocol) fornece uma ligacao bidirecional, fiavel e orientada a stream entre dois endpoints. Ao contrario de HTTP (que e stateless e requer nova ligacao por request), TCP mantem a ligacao aberta — ideal para comunicacao continua entre Gateway e Servidor.

**Protocolo customizado sobre TCP:**

O projeto define um protocolo proprio na biblioteca `SharedProtocol`:

```
Cada mensagem = 1 linha JSON terminada em \n

Exemplo de envio (Gateway -> Servidor):
  {"tipo":"DATA","sensor_id":"sensor-01","payload":{"tipo_dado":"temperatura","valor":23.5},"timestamp":"2025-06-05T10:30:00Z"}\n

Exemplo de resposta (Servidor -> Gateway):
  {"tipo":"DATA_ACK","sensor_id":"sensor-01","payload":{},"timestamp":"2025-06-05T10:30:00Z"}\n
```

**Tipos de mensagem suportados** (`SharedProtocol/TiposMensagem.cs`):
- `REGISTER` / `REGISTER_OK` / `REGISTER_ERR` — Registo de sensores
- `DATA` / `DATA_ACK` — Envio de medicoes com confirmacao
- `HEARTBEAT` / `HEARTBEAT_ACK` — Keep-alive
- `ERROR` — Erros

**Porque TCP e nao HTTP neste caso:**
- **Baixa latencia**: Sem overhead de HTTP headers (estamos a enviar linhas JSON, nao requests HTTP completos).
- **Ligacao persistente**: O Gateway envia dados continuamente — reutilizar a mesma ligacao TCP e muito mais eficiente que criar nova ligacao HTTP por cada medicao.
- **ACK explicito**: O Servidor responde com `DATA_ACK` pela mesma ligacao, sabendo imediatamente se a mensagem foi recebida.
- **Multi-gateway**: O Servidor aceita multiplas ligacoes TCP simultaneas (uma por Gateway), cada uma com a sua thread.

**Mecanismo de reconexao (TCP Watchdog):**
- `WatchdogWorker()` verifica a cada 30 segundos se a ligacao ao Servidor esta ativa.
- Se a ligacao cair, `ReconectarAsync()` tenta reconectar ate 20 vezes com delay de 5 segundos entre tentativas.
- O mutex `serverLock` protege o acesso concorrente ao stream.

**Fonte no codigo:**
- `src/Gateway/Program.cs:246-316` — `SendToServerAsync()` envia JSON + `\n` e aguarda ACK
- `src/Gateway/Program.cs:318-360` — `ReconectarAsync()` com retry ate 20 tentativas
- `src/Gateway/Program.cs:362-396` — `WatchdogWorker()` thread de monitorizacao
- `src/Servidor/Program.cs:420-485` — `HandleGateway()` aceita ligacao TCP e processa mensagens

### 4. Chamadas RPC via HTTP — Comunicacao Sincrona

**Onde e usado:**
- **Gateway --> PreProcessamento** (porta 5001): Uniformizar e validar dados antes de enviar ao Servidor
- **Servidor --> Analise** (porta 6001): Calcular estatisticas, detetar padroes, prever riscos

**Conceito fundamental:** RPC (Remote Procedure Call) permite chamar funcoes num servidor remoto como se fossem locais. Neste projeto usamos RPC sobre HTTP/JSON — o cliente envia um POST com payload JSON e aguarda a resposta JSON.

**Diferenca entre RPC e o resto da comunicacao:**

| Aspeto | RabbitMQ (AMQP) | TCP Direto | RPC HTTP |
|--------|-----------------|------------|----------|
| Padrao | Pub/Sub assincrono | Stream persistente | Request/Response |
| Resposta? | Nao (fire-and-forget) | Sim (ACK) | Sim (resultado) |
| Quando usar | Publicar dados | Comunicacao continua | Operacoes que precisam de resposta |
| Exemplo | Sensor publica medicao | Gateway envia ao Servidor | Gateway pede uniformizacao |

**Endpoints RPC implementados:**

| Rota | Metodo | Funcao | Cliente |
|------|--------|--------|---------|
| `POST /rpc/uniformizar` | Uniformizar valor para unidade padrao | Gateway (PreProcessamentoClient) |
| `POST /rpc/validar` | Validar se valor esta dentro de limites | Gateway (PreProcessamentoClient) |
| `POST /rpc/estatisticas` | Calcular media, mediana, desvio, etc. | Servidor (AnaliseClient) |
| `POST /rpc/padroes` | Detetar anomalias (z-score) e tendencias | Servidor (AnaliseClient) |
| `POST /rpc/previsao` | Prever proximos valores e riscos | Servidor (AnaliseClient) |

**Retry com backoff:** Ambos os clientes RPC (`PreProcessamentoClient` e `AnaliseClient`) implementam retry automatico — 2 tentativas com delay progressivo (1s, 2s) se o servico estiver temporariamente indisponivel.

**Fonte no codigo:**
- `src/Gateway/PreProcessamentoClient.cs:43-62` — `ComRetryAsync()` com backoff
- `src/Gateway/PreProcessamentoClient.cs:64-86` — `UniformizarDadosAsync()` POST /rpc/uniformizar
- `src/Servidor/AnaliseClient.cs:72-91` — `ComRetryAsync()` com backoff
- `src/Servidor/AnaliseClient.cs:93-157` — Endpoints de estatisticas, padroes e previsao

### 5. gRPC (proto/) — Evolucao Futura

**Onde esta definido:** `proto/analise.proto` e `proto/pre_processamento.proto`

Os ficheiros `.proto` estao atualmente marcados como desatualizados — a implementacao atual usa JSON-RPC sobre HTTP. Contudo, mantêm-se como referencia de desenho da interface RPC.

**Porque gRPC seria uma evolucao natural:**
- **HTTP/2**: Multiplexagem de streams numa unica ligacao (mais eficiente que HTTP/1.1).
- **Protobuf binario**: Serializacao muito mais compacta e rapida que JSON.
- **Streaming bidirecional**: O Servidor poderia enviar dados de analise em tempo real ao Gateway.
- **Contrato forte**: O ficheiro `.proto` define tipagem estatica — erros detetados em compilacao.
- **Suporte nativo em .NET e Python**: Ambas as linguagens do projeto suportam gRPC nativamente.

### 6. Resumo: Quando Usar Cada Paradigma

```
┌─────────────┬──────────────────────┬─────────────────────────────────────┐
│ Paradigma   │ No Projeto           │ Quando Usar                         │
├─────────────┼──────────────────────┼─────────────────────────────────────┤
│ AMQP        │ Sensor -> Gateway    │ Publicar dados, desacoplar          │
│ (RabbitMQ)  │                      │ componentes, buffering, fan-out     │
├─────────────┼──────────────────────┼─────────────────────────────────────┤
│ TCP         │ Gateway -> Servidor  │ Comunicacao persistente, baixa      │
│ (direto)    │                      │ latencia, stream de dados continuo  │
├─────────────┼──────────────────────┼─────────────────────────────────────┤
│ RPC HTTP    │ Gateway -> PreProc   │ Operacoes que precisam de           │
│ (JSON)      │ Servidor -> Analise  │ resposta imediata (request/respose) │
├─────────────┼──────────────────────┼─────────────────────────────────────┤
│ gRPC        │ (proto/, futuro)     │ Alternativa ao RPC HTTP com         │
│ (protobuf)  │                      │ melhor performance e tipagem forte  │
└─────────────┴──────────────────────┴─────────────────────────────────────┘
```
