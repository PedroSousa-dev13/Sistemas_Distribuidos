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
