# RabbitMQ Implementation - Arquitetura Visual

## Antes (TCP Direto)

```
┌─────────┐                              ┌────────┐
│ Sensor  │ ──TCP──────────────────────► │Gateway │
│ (Port   │  Connection-oriented         │(Port   │
│ Dynamic)│                              │ 5000)  │
└─────────┘                              └────────┘
                                              │
                                              ▼
                                        ┌─────────┐
                                        │ Server  │
                                        │ (6000)  │
                                        └─────────┘

Problemas:
❌ Uma conexão por sensor
❌ Sem persistência
❌ Sem reconexão automática
❌ Difícil de escalar
❌ Sem monitoramento nativo
```

## Depois (RabbitMQ Pub/Sub)

```
                    ┌──────────────────────────┐
                    │   RABBITMQ (Port 5672)   │
                    │  ┌────────────────────┐  │
                    │  │  Exchanges:        │  │
┌──────────┐        │  │  • sensor-meas     │  │
│ Sensor 1 │        │  │    (Topic)         │  │
│sensor-01 │─┐      │  │  • sensor-control  │  │
└──────────┘ │      │  │    (Direct)        │  │
             │      │  └────────────────────┘  │
┌──────────┐ │      │  ┌────────────────────┐  │
│ Sensor 2 │ │      │  │  Queues:           │  │
│sensor-02 │─┼─────►│  │  • gateway-meas    │  │
└──────────┘ │      │  │  • gateway-control │  │
             │      │  └────────────────────┘  │
┌──────────┐ │      └──────────────────────────┘
│ Sensor N │ │              ▲
│sensor-03 │─┘              │
└──────────┘                │
     Publish           Consume
  sensor.*.#          sensor.*.#
                           │
                      ┌────────────┐
                      │  GATEWAY   │
                      │  (Async)   │
                      └────────────┘
                           │
                           ▼
                      ┌────────────┐
                      │  SERVER    │
                      │  (6000)    │
                      └────────────┘

Vantagens:
✅ Desacoplado completamente
✅ Escalável horizontalmente
✅ Reconexão automática
✅ Persistência de mensagens
✅ Monitoramento integrado
✅ Suporte a múltiplos consumidores
✅ Routing flexível
✅ Queue de prioridade possível
```

## Fluxo de Mensagens

### 1. Registro (REGISTER)

```
Sensor                RabbitMQ (sensor-control)         Gateway
  │                            │                          │
  │──publish(REGISTER)───────► │                          │
  │   routing_key: "register"  │                          │
  │                            │──deliver──────────────► │
  │                            │                          │
  │                            │                    (enqueue)
  │                            │◄──ACK──────────────  │
  │                            │                          │
  └◄────────────────────────────────────────────────────  │
                               (process + confirm)
```

### 2. Medição (DATA)

```
Sensor                RabbitMQ (sensor-measurements)    Gateway
  │                            │                         │
  │──publish(DATA)────────────►│                         │
  │   topic: sensor.01.temp    │                         │
  │                            │──deliver──────────────►│
  │                            │                         │
  │                            │                   (enqueue)
  │                            │                         │
  │                            │◄──ACK─────────────────│
  │                            │                         │
  │                            │                 (RPC process)
  │                            │                         │
  │                            │                 (validate)
  │                            │                         │
  │                            │          (send to server)
  │                            │                         │
  └◄────────────────────────────────────────────────────│
                         (heartbeat maint.)
```

### 3. Heartbeat (HEARTBEAT)

```
Sensor (10s interval)    RabbitMQ (sensor-control)     Gateway
  │                              │                       │
  ├──publish(HEARTBEAT)─────────►│                       │
  │   routing_key: "heartbeat"   │                       │
  │                              │──deliver────────────►│
  │                              │                       │
  │                              │                  (update)
  │                              │◄──ACK───────────────│
  │                              │                       │
  └─ (repeat in 10s)
```

## Topologia Detalhada

```
┌─────────────────────────────────────────────────────────┐
│              RABBITMQ BROKER (5672/AMQP)               │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │     EXCHANGE: sensor-measurements (Topic)         │  │
│  │     Durable: true, AutoDelete: false             │  │
│  │     ┌────────────────────────────────────────┐   │  │
│  │     │ Bindings:                              │   │  │
│  │     │ • Queue: gateway-measurements-*        │   │  │
│  │     │   Key: sensor.*.#                      │   │  │
│  │     └────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │     EXCHANGE: sensor-control (Direct)            │  │
│  │     Durable: true, AutoDelete: false             │  │
│  │     ┌────────────────────────────────────────┐   │  │
│  │     │ Bindings:                              │   │  │
│  │     │ • Queue: gateway-control-*             │   │  │
│  │     │   Key: # (match all)                   │   │  │
│  │     └────────────────────────────────────────┘   │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │     QUEUE: gateway-measurements-{gateway_id}     │  │
│  │     • Durable, Non-exclusive                     │  │
│  │     • Consumers: 1 (Gateway async)               │  │
│  │     • Ack Mode: Auto ACK                         │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
│  ┌──────────────────────────────────────────────────┐  │
│  │     QUEUE: gateway-control-{gateway_id}          │  │
│  │     • Durable, Non-exclusive                     │  │
│  │     • Consumers: 1 (Gateway async)               │  │
│  │     • Ack Mode: Auto ACK                         │  │
│  └──────────────────────────────────────────────────┘  │
│                                                         │
└─────────────────────────────────────────────────────────┘
```

## Routing Examples

### Exemplo 1: Múltiplos Sensores

```
Sensor-01 publica:           Sensor-02 publica:
  sensor.sensor-01.temp  ───►  Exchange      ◄─── sensor.sensor-02.humidade
  sensor.sensor-01.humid      sensor-meas
  sensor.sensor-01.ar    ───►  (Topic)       ◄─── sensor.sensor-02.humidade
                                 │
                                 │ (matches sensor.*.#)
                                 ▼
                           Queue: gateway-msg
                           Consumer: Gateway
```

### Exemplo 2: Filtragem por Tipo (Extensão Futura)

```
Se implementar queue por tipo:

  sensor.sensor-01.temperatura  ──┐
  sensor.sensor-02.temperatura  ──┼──► Queue: temperaturas
                                   │    Consumer: AnalysisService
  
  sensor.sensor-01.humidade  ────┐
  sensor.sensor-02.humidade  ────┼──► Queue: humidades
                                  │    Consumer: AlertService
```

## Diagrama de Sequência: Fluxo Completo

```
┌─────────┐   ┌──────────┐   ┌───────────┐   ┌────────┐
│ Sensor  │   │ RabbitMQ │   │ Gateway   │   │ Server │
└────┬────┘   └────┬─────┘   └─────┬─────┘   └───┬────┘
     │             │               │            │
     │ 1. Connect  │               │            │
     ├─────────────►               │            │
     │             │               │            │
     │ 2. Declare  │               │            │
     ├─────────────►               │            │
     │             │               │            │
     │             │  3. Consumer  │            │
     │             │◄──────────────┤            │
     │             │               │            │
     │ 4. Publish  │               │            │
     ├─────────────►               │            │
     │             │               │            │
     │             │ 5. Deliver    │            │
     │             ├──────────────►            │
     │             │               │            │
     │             │               │ 6. Process │
     │             │               │   & ACK    │
     │             │               │            │
     │             │               │ 7. RPC    │
     │             │               ├────────────►
     │             │               │            │
     │             │               │◄───────────┤
     │             │               │            │
     │             │               │ 8. Send   │
     │             │               ├───────────►│
     │             │               │            │
     │             │               │           ack
     │             │               │◄──────────┤
     │             │               │            │
     │ 9. HB       │               │            │
     ├─────────────►               │            │
     │             │               │            │
     │             │ 10. HB Deliver│           │
     │             ├──────────────►            │
     │             │               │            │
     │             │              (update sync)│
     │             │               │            │
```

## Arquivo de Configuração

### docker-compose.yml Topology

```yaml
rabbitmq:
  - Image: rabbitmq:3.13-management
  - Ports:
    - 5672 (AMQP Protocol)
    - 15672 (Management Web UI)
  - Volumes:
    - rabbitmq_data (persistent)
  - Environment:
    - Default: guest/guest
```

---

**Arquitetura implementada e validada em 2026-05-18** ✅
