# RabbitMQ Routing Strategy - Sistema IoT Distribuído

## Topologia de Exchanges e Queues

### Exchanges
1. **sensor-measurements** (Topic Exchange)
   - Utilizado para medições de dados dos sensores
   - Routing keys: `sensor.{sensor_id}.{data_type}`
   - Exemplo: `sensor.sensor-01.temperatura`, `sensor.sensor-02.humidade`

2. **sensor-control** (Direct Exchange)
   - Utilizado para mensagens de controle (registro, heartbeat)
   - Routing keys: `register`, `heartbeat`

### Queues (Gateway)
- **gateway-measurements-{gateway_id}**: Recebe todas as medições (binding com `sensor.*.#`)
- **gateway-control-{gateway_id}**: Recebe eventos de controle (binding com `#`)

## Routing por Tipo de Dado e Zona

### Estratégia Implementada

#### 1. Routing por Sensor e Tipo de Dado
```
Topic: sensor.{sensor_id}.{data_type}

Exemplos:
- sensor.sensor-01.temperatura
- sensor.sensor-01.humidade
- sensor.sensor-02.qualidade_ar
- sensor.sensor-03.ruido
```

#### 2. Informação de Zona
A zona de cada sensor é armazenada no CSV (`sensores.csv`):
```
sensor-01|ativo|Sala_A|[temperatura,humidade,qualidade_ar,ruido,pm25,pm10,luminosidade,imagem]|2026-04-21T01:04:25.0342482Z
sensor-02|manutencao|Sala_B|[temperatura,humidade]|2026-04-19T16:23:23.0075699+01:00
sensor-03|manutencao|Corredor|[ruido,humidade]|2026-04-19T16:06:44.2225096+01:00
```

#### 3. Filtragem no Gateway
O Gateway pode filtrar mensagens por zona internamente, acessando `sensors[sensorId].Zona`

### Exemplo de Fluxo

```
Sensor (sensor-01 em Sala_A)
    └─ Publica: sensor.sensor-01.temperatura = 22.5
       ↓
RabbitMQ (Exchange: sensor-measurements)
    └─ Routing Key: sensor.sensor-01.temperatura
       ↓
Gateway (Queue: gateway-measurements-gateway-xxx)
    └─ Recebe mensagem
    └─ Busca Zona: Sala_A
    └─ Valida e processa
    └─ Encaminha ao Servidor
```

## Extensões Futuras

Para suporte mais avançado de routing por zona, recomenda-se:

1. **Exchange adicional por zona**
   ```
   - zona-sala_a (Topic Exchange)
   - zona-corredor (Topic Exchange)
   - zona-outras (Topic Exchange)
   ```

2. **Fila segregada por zona**
   ```
   - gateway-sala_a-queue
   - gateway-corredor-queue
   - gateway-outras-queue
   ```

3. **Consumidores especializados**
   Cada consumidor processa dados de uma zona específica

4. **Metadata em Headers**
   Adicionar headers AMQP com zona, tipo de sensor, etc.
   ```csharp
   properties.Headers = new Dictionary<string, object>
   {
       ["zona"] = sensorZone,
       ["sensor_type"] = sensorType,
       ["data_class"] = dataClass
   };
   ```

## Configuração de Conexão RabbitMQ

### Sensor
```bash
Sensor sensor-01 [rabbitmq_host] [rabbitmq_port]
Exemplo:
  Sensor sensor-01 localhost 5672
  Sensor sensor-01 192.168.1.100 5672
```

### Gateway
```bash
Gateway <serverEndpoint> <csvPath> [rabbitmq_host] [rabbitmq_port]
Exemplo:
  Gateway 127.0.0.1:6000 ./sensores.csv localhost 5672
  Gateway 192.168.1.200:6000 ./sensores.csv rabbitmq-server 5672
```

## RabbitMQ Management UI
- URL: http://localhost:15672
- Usuário: guest
- Senha: guest

Pode monitorar exchanges, queues e mensagens em tempo real.
