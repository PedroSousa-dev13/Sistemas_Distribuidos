# 🎯 FASE 3 - DOCUMENTAÇÃO COMPLETA

## TP2: Serviços de Análise e Monitorização Urbana para One Health

**Data:** 20 de maio de 2026  
**Status:** ✅ 100% Completo  
**Branch:** `feature/fase-3-complete`

---

## 📋 Sumário Executivo

A **Fase 3** implementa a camada de análise e pré-processamento do sistema IoT distribuído. O sistema agora integra:

- ✅ **Serviço de Pré-Processamento (RPC)** - Uniformização e validação de dados
- ✅ **Serviço de Análise (RPC)** - Estatísticas e previsão de anomalias
- ✅ **Docker Compose Expandido** - Orquestração completa com .NET 8.0
- ✅ **Testes de Integração** - Validação end-to-end
- ✅ **Build System Corrigido** - Compatibilidade com .NET 8.0

---

## 🏗️ Arquitetura da Fase 3

```
┌─────────────┐
│   SENSOR    │  Publica dados brutos (JSON/CSV/etc)
└──────┬──────┘
       │
       ▼
┌─────────────────────────────────┐
│  RABBITMQ (Message Broker)      │  Topic exchanges + routing
│  - sensor-measurements          │
│  - sensor-control               │
└────────────────┬────────────────┘
                 │
                 ▼
         ┌───────────────┐
         │    GATEWAY    │  Orquestra o fluxo
         └───────┬───────┘
                 │
    ┌────────────┼────────────┐
    ▼            ▼            ▼
PRE-PROC      SERVER      ANÁLISE
(5001)        (6000)      (6001)
- Uniformizar  - Armazena - Estatísticas
- Validar      - RPC calls - Padrões
```

---

## 📁 Estrutura de Ficheiros Modificados/Criados

### 1. Downgrade para .NET 8.0

```diff
- <TargetFramework>net9.0</TargetFramework>
+ <TargetFramework>net8.0</TargetFramework>
```

**Ficheiros atualizados:**
- ✅ `src/SharedProtocol/SharedProtocol.csproj`
- ✅ `src/Sensor/Sensor.csproj`
- ✅ `src/Gateway/Gateway.csproj`
- ✅ `src/DataStreamClient/DataStreamClient.csproj`
- ✅ `src/Servidor/Servidor.csproj`
- ✅ `tests/Gateway.Tests/Gateway.Tests.csproj`
- ✅ `tests/Servidor.Tests/Servidor.Tests.csproj`
- ✅ `.vscode/launch.json` (4 referências atualizadas)
- ✅ `TESTE_COMPLETO.md` (verificação de bins)

### 2. Docker Compose Expandido

**Ficheiro:** `docker-compose.yml`

Serviços adicionados:

```yaml
services:
  rabbitmq:
    # RabbitMQ 3.13 com Management UI (15672)
  
  pre-processamento:
    # Python 3.12 + servico.py (porta 5001)
    # Endpoints: /rpc/uniformizar, /rpc/validar
  
  analise:
    # Python 3.12 + servico.py (porta 6001)
    # Endpoints: /rpc/estatisticas, /rpc/padroes, /rpc/previsao
```

### 3. Dockerfiles para Serviços Python

**Ficheiro:** `Dockerfile.preprocessing`
```dockerfile
FROM python:3.12-slim
COPY src/PreProcessamento/servico.py /app/servico.py
EXPOSE 5001
CMD ["python", "servico.py"]
```

**Ficheiro:** `Dockerfile.analysis`
```dockerfile
FROM python:3.12-slim
COPY src/Analise/servico.py /app/servico.py
COPY src/Analise/analise_estatistica.py /app/analise_estatistica.py
EXPOSE 6001
CMD ["python", "servico.py"]
```

### 4. Testes de Integração

**Ficheiro:** `tests/test_integracao.py`

Testes implementados:
- ✅ **TEST 1:** Pre-Processamento Service
  - Health check (porta 5001)
  - Uniformização (Fahrenheit → Celsius)
  - Validação de valores
  
- ✅ **TEST 2:** Análise Service
  - Health check (porta 6001)
  - Cálculo de estatísticas
  - Detecção de anomalias
  - Previsão/forecasting
  
- ✅ **TEST 3:** RabbitMQ Setup
  - Conectividade
  - Verificação de queues
  
- ✅ **TEST 4:** Data Flow Integration
  - Fluxo completo: dados brutos → uniformização → validação → análise

---

## 🚀 Quick Start - Como Executar

### Pré-requisitos

```bash
# Verificar .NET 8.0
dotnet --version  # Deve ser 8.0.x

# Verificar Python 3.12+
python --version  # Deve ser 3.12+

# Verificar Docker e Docker Compose
docker --version
docker-compose --version
```

### 1. Build do Projeto

```bash
# Na raiz do projeto
dotnet build SharedProtocol.sln -c Debug

# Ou fazer clean + build
dotnet clean SharedProtocol.sln
dotnet build SharedProtocol.sln -c Debug
```

### 2. Iniciar Infraestrutura

```bash
# Terminal 1: Iniciar RabbitMQ + Serviços Python
docker-compose up

# Ou em background
docker-compose up -d

# Verificar status
docker-compose ps

# Ver logs
docker-compose logs -f
```

### 3. Executar Componentes .NET

```bash
# Terminal 2: Servidor (porta 6000)
dotnet run --project src/Servidor/Servidor.csproj -- 127.0.0.1 6000

# Terminal 3: Gateway (portas RabbitMQ 5672, Server 6000)
dotnet run --project src/Gateway/Gateway.csproj -- 127.0.0.1:6000 ./sensores.csv localhost 5672

# Terminal 4: Sensor (RabbitMQ 5672)
dotnet run --project src/Sensor/Sensor.csproj -- sensor-01 localhost 5672
```

### 4. Testar Sistema

```bash
# Enviar dados do sensor (menu interativo)
# No sensor, escolher: 1 (temperatura)
# Introduzir: 77 (Fahrenheit)

# Verificar no Gateway:
# [PreProcessamento] Uniformizar: 77°F -> 25°C
# [PreProcessamento] Validar: 25°C - VALIDO
# [DATA] Encaminhado ao servidor com sucesso

# Verificar no Servidor (menu)
# > stats temperatura
# [Análise] Media: 25°C, Desvio: X, ...
```

### 5. Testes de Integração

```bash
# Correr testes de integração
python tests/test_integracao.py

# Resultado esperado:
# ✓ TEST 1: Pre-Processamento Service - PASS
# ✓ TEST 2: Análise Service - PASS
# ✓ TEST 3: RabbitMQ Setup - PASS
# ✓ TEST 4: Data Flow Integration - PASS
# Total: 4/4 tests passed
```

---

## 📊 Endpoints RPC

### Pre-Processamento (Porto 5001)

| Endpoint | Método | Payload | Response |
|----------|--------|---------|----------|
| `/rpc/uniformizar` | POST | `{"sensor_id", "tipo_dado", "valor", "timestamp", "formato_original"}` | `{"sucesso", "valor_uniformizado", "unidade"}` |
| `/rpc/validar` | POST | `{"sensor_id", "tipo_dado", "valor"}` | `{"valido", "erros"}` |

**Exemplo - Uniformizar:**
```bash
curl -X POST http://localhost:5001/rpc/uniformizar \
  -H "Content-Type: application/json" \
  -d '{
    "sensor_id": "sensor-01",
    "tipo_dado": "temperatura",
    "valor": 212.0,
    "timestamp": "2026-05-20T10:00:00Z",
    "formato_original": "FAHRENHEIT"
  }'

# Response:
# {"sucesso": true, "valor_uniformizado": 100.0, "unidade": "celsius"}
```

### Análise (Porto 6001)

| Endpoint | Método | Payload | Response |
|----------|--------|---------|----------|
| `/rpc/estatisticas` | POST | `{"sensor_id", "tipo_dado", "valores": [...]}` | `{"media", "mediana", "desvio_padrao", "variancia", ...}` |
| `/rpc/padroes` | POST | `{"sensor_id", "tipo_dado", "valores": [...]}` | `{"anomalias", "tendencia", "total_anomalias"}` |
| `/rpc/previsao` | POST | `{"sensor_id", "tipo_dado", "valores": [...]}` | `{"proximo_valor", "tendencia", "risco", "previsoes"}` |

**Exemplo - Estatísticas:**
```bash
curl -X POST http://localhost:6001/rpc/estatisticas \
  -H "Content-Type: application/json" \
  -d '{
    "sensor_id": "sensor-01",
    "tipo_dado": "temperatura",
    "valores": [20.0, 21.0, 22.0, 23.0, 24.0]
  }'

# Response:
# {
#   "sucesso": true,
#   "media": 22.0,
#   "mediana": 22.0,
#   "desvio_padrao": 1.4142,
#   "minimo": 20.0,
#   "maximo": 24.0,
#   ...
# }
```

---

## 🧪 Testes Unitários Existentes

### Python

```bash
# Pré-Processamento (21 testes)
python -m pytest tests/test_pre_processamento.py -v

# Análise (18 testes)
python -m pytest tests/test_analise_estatistica.py -v
```

### C#

```bash
# Gateway Tests
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj

# Servidor Tests
dotnet test tests/Servidor.Tests/Servidor.Tests.csproj
```

---

## 🐛 Troubleshooting

### Erro: "Cannot connect to Pre-Processamento (port 5001)"

```bash
# Verificar se container está running
docker-compose ps

# Se não estiver, iniciar
docker-compose up -d pre-processamento

# Ver logs
docker-compose logs pre-processamento
```

### Erro: ".NET SDK does not support targeting .NET 9.0"

```bash
# Verificar .NET SDK instalado
dotnet --version

# Deve ser 8.0.x ou superior
# Se for 8.0.x, limpar cache e rebuild
dotnet clean
rm -rf bin obj
dotnet build SharedProtocol.sln -c Debug
```

### Erro: RabbitMQ connection refused

```bash
# Verificar RabbitMQ status
docker-compose ps rabbitmq

# Restartar RabbitMQ
docker-compose restart rabbitmq

# Ou iniciar do zero
docker-compose down
docker-compose up -d
```

### Sensores não recebem dados

```bash
# 1. Verificar RabbitMQ Management UI
# http://localhost:15672 (guest/guest)
# - Ver exchanges: sensor-measurements, sensor-control
# - Ver queues: gateway-measurements-*, gateway-control-*

# 2. Verificar Gateway logs
# Deve estar subscrevendo e processando mensagens

# 3. Restartar tudo
docker-compose down
dotnet clean
docker-compose up
```

---

## ✅ Checklist de Validação

- [x] .NET 8.0 downgrade em todos os .csproj
- [x] Docker-compose com RabbitMQ + Pre-Processamento + Análise
- [x] Dockerfiles para serviços Python (5001, 6001)
- [x] Testes de integração implementados (4 testes)
- [x] Build com sucesso (`dotnet build`)
- [x] Endpoints RPC documentados
- [x] Testes unitários passando
- [x] Sistema testado end-to-end
- [x] Documentação completa

---

## 📚 Referências Adicionais

### Ficheiros Principais da Fase 3

- [src/PreProcessamento/servico.py](src/PreProcessamento/servico.py) - RPC Pre-Processamento
- [src/Analise/servico.py](src/Analise/servico.py) - RPC Análise
- [src/Analise/analise_estatistica.py](src/Analise/analise_estatistica.py) - Funções de análise
- [src/Gateway/PreProcessamentoClient.cs](src/Gateway/PreProcessamentoClient.cs) - Client RPC
- [src/Servidor/AnaliseClient.cs](src/Servidor/AnaliseClient.cs) - Client RPC

### Documentação Anterior

- [INDEX.md](INDEX.md) - Índice completo do projeto
- [ARQUITETURA.md](ARQUITETURA.md) - Diagramas visuais
- [ROUTING_STRATEGY.md](ROUTING_STRATEGY.md) - Estratégia RabbitMQ
- [RABBITMQ_README.md](RABBITMQ_README.md) - Quick start RabbitMQ

---

## 👥 Notas de Desenvolvimento

### Branch Strategy

- `main` - Branch principal estável
- `feature/fase-3-complete` - Branch de desenvolvimento (atual)

### Commits Recomendados

```bash
# 1. Downgrade .NET 8.0
git commit -m "refactor: downgrade to .NET 8.0 for SDK compatibility"

# 2. Docker improvements
git commit -m "feat: add Docker support for Python services"

# 3. Integration tests
git commit -m "test: add comprehensive integration tests for Fase 3"

# 4. Merge para main
git checkout main
git merge feature/fase-3-complete
git push origin main
```

### Próximas Melhorias (Nice-to-Have)

- [ ] Kubernetes deployment files
- [ ] Prometheus metrics/monitoring
- [ ] Swagger/OpenAPI documentation
- [ ] Database persistence (PostgreSQL)
- [ ] Web UI para visualização
- [ ] CI/CD pipeline (GitHub Actions)

---

## 🎓 Conclusão

A **Fase 3** está completa e pronta para produção. O sistema implementa:

1. ✅ **Processamento de dados** com pré-processamento automático
2. ✅ **Análise estatística** de séries temporais
3. ✅ **Detecção de anomalias** baseada em Z-Score
4. ✅ **Previsão** com regressão linear
5. ✅ **Orquestração completa** com Docker Compose
6. ✅ **Testes abrangentes** (unitários + integração)

O projeto está pronto para continuar para a próxima fase ou ser deployado em produção.

---

**Última atualização:** 20 de maio de 2026  
**Status:** ✅ Pronto para Produção
