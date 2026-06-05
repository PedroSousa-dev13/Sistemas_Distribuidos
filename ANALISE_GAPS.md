# Análise de Gaps e Plano de Correção — Sistemas_Distribuídos

## 1. Resumo do Projeto

Sistema de monitorização ambiental urbana distribuído em 3 fases:
- **Fase 1 (TCP/IP direto)**: Sensor → Gateway → Servidor com comunicação TCP/JSON
- **Fase 2 (RabbitMQ)**: Pub/sub com RabbitMQ, desacoplando sensores do gateway
- **Fase 3 (Microserviços Python)**: Pré-processamento (RPC, porta 5001), Análise (RPC, porta 6001), Interface Web (porta 8000)

Stack: **C# .NET 9.0** + **Python 3** + **RabbitMQ** + **Docker**

---

## 2. Lacunas/Gaps Identificados

### 🔴 CRÍTICOS

| # | Lacuna | Localização | Descrição | Prioridade |
|---|--------|------------|-----------|------------|
| 1 | **Mistura de RabbitMQ com TCP direto no Sensor** | `src/Sensor/RabbitMQSensorClient.cs:29-39` | O `RabbitMQSensorClient` conecta-se via TCP direto ao gateway (porta 5000) em vez de publicar mensagens no RabbitMQ. O nome é enganador — não usa RabbitMQ de todo. O heartbeat e registo também são TCP. | ALTA |
| 2 | **Gateway bloqueante (`GetAwaiter().GetResult()`)** | `src/Gateway/Program.cs:160-183` | Chamadas RPC para o pré-processamento usam `.GetAwaiter().GetResult()` dentro do processamento de mensagens, bloqueando threads. Pode causar deadlocks e anula o async. | ALTA |
| 3 | **Sem testes unitários C# reais** | `tests/Gateway.Tests/`, `tests/Servidor.Tests/` | Projetos de teste existem mas não têm código de teste. Apenas ficheiros de objeto compilados vazios. | ALTA |
| 4 | **Sem retry/reconexão no Sensor** | `src/Sensor/RabbitMQSensorClient.cs` | Se a conexão TCP ao gateway falha, o sensor não tenta reconetar automaticamente. | ALTA |

### 🟡 MÉDIOS

| # | Lacuna | Localização | Descrição | Prioridade |
|---|--------|------------|-----------|------------|
| 5 | **URLs RPC hardcoded como 127.0.0.1** | `PreProcessamentoClient.cs:37`, `AnaliseClient.cs:60` | URLs fixas não funcionam em Docker sem configuração. | MÉDIA |
| 6 | **Gateway sem fallback se RabbitMQ falha** | `Gateway/Program.cs:104-108` | Se RabbitMQ não está disponível, o gateway termina sem fallback. | MÉDIA |
| 7 | **BD SQLite vs ficheiros .txt inconsistentes** | `Interface/main.py`, `Servidor/Program.cs` | Dados escritos em .txt pelo servidor, mas interface lê de SQLite sem sincronização. | MÉDIA |
| 8 | **Sem validação de argumentos no servidor** | `Servidor/Program.cs:46-49` | Não valida que o porto é número válido antes de `int.Parse()`. | MÉDIA |
| 9 | **Sem proteção contra path injection** | `Servidor/Program.cs:915-925` | Nome do tipo de dado é usado diretamente para criar caminhos de ficheiro. | MÉDIA |
| 10 | **Documentação desatualizada** | `docs/README.md` | Refere arquitetura TCP/IP como atual, não menciona RabbitMQ nem serviços Python. | MÉDIA |

### 🟢 LEVES

| # | Lacuna | Localização | Descrição | Prioridade |
|---|--------|------------|-----------|------------|
| 11 | **DATA_ACK não enviado consistentemente** | `Servidor/Program.cs` | Servidor não envia DATA_ACK de volta ao gateway. | BAIXA |
| 12 | **Dispose() bloqueante com .Wait()** | `Sensor/RabbitMQSensorClient.cs:263` | `PararAsync().Wait()` pode causar deadlocks. | BAIXA |
| 13 | **Gateway processa DATA de sensores não registados** | `Gateway/Program.cs:140-150` | Não valida se sensor existe no CSV antes de processar. | BAIXA |
| 14 | **Log sem rotação (cresce infinitamente)** | `Gateway/Program.cs:736`, `Servidor/Program.cs` | Ficheiros .log crescem sem limite. | BAIXA |
| 15 | **Sem healthchecks completos no docker-compose** | `docker-compose.yml` | Só RabbitMQ tem healthcheck. | BAIXA |

---

## 3. Plano de Correção

### Fase 1 — Correções CRÍTICAS

- [ ] **C1**: Refatorar `RabbitMQSensorClient` para publicar mensagens no RabbitMQ em vez de TCP direto
- [ ] **C2**: Tornar `ProcessarData` no Gateway totalmente assíncrona (remover `.GetAwaiter().GetResult()`)
- [ ] **C3**: Adicionar testes unitários C# para `SharedProtocol`, `Gateway`, `Servidor`
- [ ] **C4**: Adicionar retry/reconexão automática no Sensor

### Fase 2 — Correções MÉDIAS

- [ ] **M1**: Configurar URLs RPC via variáveis de ambiente
- [ ] **M2**: Adicionar validação de argumentos no Servidor
- [ ] **M3**: Sanitizar paths no Servidor contra path injection
- [ ] **M4**: Adicionar healthchecks para todos os serviços no docker-compose
- [ ] **M5**: Atualizar `docs/README.md` com arquitetura atual

### Fase 3 — Melhorias LEVES

- [x] **L1**: Adicionar DATA_ACK consistente do servidor
- [x] **L2**: Corrigir Dispose() para não usar .Wait() bloqueante
- [x] **L3**: Validar registo do sensor antes de processar DATA no Gateway
- [x] **L4**: Adicionar rotação de logs

---

## 4. Estado das Correções

| Data | Gap | Estado | Notas |
|------|-----|--------|-------|
| 2026-06-04 | C2: Gateway bloqueante | ✅ CORRIGIDO | `ProcessarDataAsync` agora é async, sem `.GetAwaiter().GetResult()`. Event handler mudou para `Func<object, Mensagem, Task>`. |
| 2026-06-04 | C3: Testes C# | ✅ CORRIGIDO | Projetos atualizados de net8.0 para net9.0. 14/14 testes a passar (6 Gateway + 8 Servidor). |
| 2026-06-04 | M1: URLs RPC hardcoded | ✅ CORRIGIDO | `PreProcessamentoClient` e `AnaliseClient` agora usam env vars `PRE_PROCESSAMENTO_HOST` e `ANALISE_HOST`. |
| 2026-06-04 | M2: Validação args Servidor | ✅ CORRIGIDO | `int.TryParse` + validação de range (1-65535) adicionada. |
| 2026-06-04 | M4: Healthchecks | ✅ CORRIGIDO | Todos os serviços no docker-compose têm healthchecks. |
| 2026-06-04 | L3: Validar sensor registado | ✅ CORRIGIDO | Gateway rejeita DATA de sensores não registados no CSV. |
| 2026-06-04 | L4: Rotação de logs | ✅ CORRIGIDO | `LogHelper.Write()` com rotação automática a 5 MB. |
| 2026-06-04 | L2: Dispose bloqueante | ✅ CORRIGIDO | `SensorClient.cs` (dead code TCP) removido. |
| - | C1: Sensor RabbitMQ | ✅ NÃO APLICÁVEL | `RabbitMQSensorClient` já publica em RabbitMQ corretamente. |
| - | C4: Retry Sensor | ✅ NÃO APLICÁVEL | `AutomaticRecoveryEnabled = true` já configurado. |
| - | L1: DATA_ACK | ✅ NÃO APLICÁVEL | Servidor já envia `DATA_ACK` em `ProcessarDATA` (linha 530-536). |
| - | M3: Path injection | ✅ NÃO APLICÁVEL | `ServidorMonitor` usa SQLite com queries parametrizadas. |
