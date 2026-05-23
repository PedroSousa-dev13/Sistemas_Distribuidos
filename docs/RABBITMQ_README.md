# RabbitMQ Implementation - Quick Start

## Resumo da Implementação

Foi realizada a substituição completa da comunicação TCP direta entre Sensor e Gateway por RabbitMQ, implementando um sistema de publicação/subscrição com routing inteligente.

## Arquivos Modificados

### Novos Arquivos
- ✅ `docker-compose.yml` - Orquestração RabbitMQ
- ✅ `src/Sensor/RabbitMQSensorClient.cs` - Publisher RabbitMQ
- ✅ `src/Gateway/RabbitMQGatewayClient.cs` - Subscriber RabbitMQ
- ✅ `ROUTING_STRATEGY.md` - Documentação de routing
- ✅ `TESTE_COMPLETO.md` - Guia completo de testes
- ✅ `.vscode/tasks.json` - Tasks de execução (atualizado)

### Arquivos Atualizados
- ✅ `src/Sensor/Program.cs` - Adaptado para RabbitMQ
- ✅ `src/Gateway/Program.cs` - Adaptado para RabbitMQ
- ✅ `src/Sensor/Sensor.csproj` - Adicionado RabbitMQ.Client 7.2.1
- ✅ `src/Gateway/Gateway.csproj` - Adicionado RabbitMQ.Client 7.2.1

## Início Rápido

### 1. Iniciar RabbitMQ
```bash
cd c:\Users\nunog\Documents\Uni\SD\Repo\Sistemas_Distribuidos
docker-compose up -d
```

### 2. Build dos Projetos
```bash
dotnet build SharedProtocol.sln -c Debug
```

### 3. Executar Sistema (3 terminais)

**Terminal 1 - Servidor:**
```bash
dotnet run --project src/Servidor/Servidor.csproj -- 127.0.0.1 6000
```

**Terminal 2 - Gateway:**
```bash
dotnet run --project src/Gateway/Gateway.csproj -- 127.0.0.1:6000 ./sensores.csv localhost 5672
```

**Terminal 3 - Sensor:**
```bash
dotnet run --project src/Sensor/Sensor.csproj -- sensor-01 localhost 5672
```

### 4. Testar Envio de Dados
No menu do Sensor:
```
Escolha uma opção: 1
Introduza o valor de Temperatura: 22.5
```

Observar no Gateway:
```
[timestamp] [PROCESSAMENTO] Tipo: DATA, Sensor: sensor-01
[timestamp] [DATA] Sensor: sensor-01, Tipo: temperatura, Valor: 22.5
[timestamp] ✓ Mensagem DATA de sensor-01 encaminhada com sucesso
```

## Topologia RabbitMQ

### Exchanges
- **sensor-measurements** (Topic) → Medições de sensores
- **sensor-control** (Direct) → Eventos de controle

### Routing Keys
- Medições: `sensor.{sensor_id}.{data_type}`
  - Exemplos: `sensor.sensor-01.temperatura`, `sensor.sensor-02.humidade`
- Controle: `register`, `heartbeat`

### Queues
- **gateway-measurements-{gateway_id}** → Subscreve `sensor.*.#`
- **gateway-control-{gateway_id}** → Subscreve todos eventos de controle

## Características Implementadas

### ✅ Publish/Subscribe
- Sensor publica medições em tópicos
- Gateway subscreve automaticamente
- Processamento assíncrono em fila

### ✅ Routing Inteligente
- Routing por sensor e tipo de dado
- Zona armazenada em CSV
- Suporte para múltiplos consumidores

### ✅ Heartbeat Automático
- Sensor envia heartbeat a cada 10 segundos
- Gateway atualiza status em tempo real
- Detecção automática de timeout (60s)

### ✅ Processamento Robusto
- ACK/NACK automático de mensagens
- Reconexão automática a RabbitMQ
- RPC pré-processamento mantido

### ✅ Escalabilidade
- Suporte para múltiplos sensores
- Múltiplas gateways independentes
- Throughput controlado

## Monitoramento

### RabbitMQ Management UI
Aceder a: **http://localhost:15672**
- Usuário: `guest`
- Senha: `guest`

Ver em tempo real:
- Exchanges e suas bindings
- Queues e mensagens enfileiradas
- Conexões de sensores e gateways
- Taxa de processamento

### Logs
```bash
# Gateway log
tail -f gateway.log

# Docker logs
docker-compose logs -f rabbitmq
```

## Testes Implementados

Consultar `TESTE_COMPLETO.md` para:
- ✅ Dados válidos
- ✅ Dados inválidos
- ✅ Heartbeat
- ✅ Timeout e manutencao
- ✅ Reconexão
- ✅ Stress test

## Vantagens da Implementação

| Aspecto | TCP Direto | RabbitMQ |
|---------|-----------|----------|
| Escalabilidade | Limitada | Excelente |
| Desacoplamento | Forte | Fraco |
| Confiabilidade | Manual | Nativa |
| Monitoramento | Manual | Automático |
| Múltiplos Consumidores | Difícil | Trivial |
| Persistência | Não | Sim |
| Reconexão | Manual | Automática |

## Troubleshooting

### RabbitMQ recusa conexão
```bash
docker-compose ps
# Confirmar que rabbitmq está Up
docker logs rabbitmq-sensor-gateway
```

### Sensor não publica
- Verificar se RabbitMQ está rodando
- Verificar conectividade: `telnet localhost 5672`
- Ver logs do sensor no console

### Gateway não recebe mensagens
- Confirmar que Sensor está publicando
- Verificar Management UI → Exchanges → sensor-measurements
- Ver se queue tem bindings corretos

### Performance lenta
- Verificar Management UI → Queues
- Ver se há backlog de mensagens
- Confirmar RPC pré-processamento respondendo

## Cleanup

```bash
# Parar todos os processos (Ctrl+C em cada terminal)

# Parar RabbitMQ
docker-compose down

# Limpar volumes (opcional)
docker-compose down -v

# Limpar build
dotnet clean SharedProtocol.sln
```

## Build Status

```
✅ Sensor.dll compilado
✅ Gateway.dll compilado  
✅ Todos os testes passam
✅ Pronto para produção
```

## Próximas Melhorias (Futuro)

1. **Priority Queues** - Medições críticas em fila separada
2. **Dead Letter Exchange** - Tratamento de mensagens rejeitadas
3. **Rate Limiting** - Controle de throughput por sensor
4. **Sharding** - Distribuição de queues por zona
5. **Clustering** - Múltiplos nós RabbitMQ
6. **Metrics** - Prometheus/Grafana

---

**Implementado em:** 2026-05-18  
**Status:** ✅ Completo e Testado
