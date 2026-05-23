# Teste Completo: Sensor -> RabbitMQ -> Gateway

## Requisitos

1. **Docker e Docker Compose instalados**
   - Windows: Docker Desktop (inclui Docker Compose)
   - Linux/Mac: `docker-compose` separado

2. **.NET 9.0 SDK instalado**

3. **Projetos compilados**

## Passo 1: Iniciar RabbitMQ

```bash
# Na raiz do projeto
docker-compose up -d

# Verificar se está rodando
docker ps | grep rabbitmq

# Ver logs
docker-compose logs -f rabbitmq
```

### Verificação
- Container `rabbitmq-sensor-gateway` deve estar `Up`
- Porta 5672 (AMQP) aberta
- Porta 15672 (Management UI) aberta

### Management UI (Optional)
- Abrir: http://localhost:15672
- Login: guest / guest
- Ver Exchanges, Queues e Connections

## Passo 2: Build dos Projetos

```bash
# Build completo
dotnet build SharedProtocol.sln -c Debug

# Ou builds individuais
dotnet build src/Sensor/Sensor.csproj -c Debug
dotnet build src/Gateway/Gateway.csproj -c Debug
```

### Verificação
```bash
# Deve haver outputs em bin/Debug
dir src/Sensor/bin/Debug/net9.0
dir src/Gateway/bin/Debug/net9.0
```

## Passo 3: Executar Server (Pré-requisito)

```bash
# Terminal 1: Server
dotnet run --project src/Servidor/Servidor.csproj -- 127.0.0.1 6000
```

**Output esperado:**
```
+------------------------------------------+
|  SERVIDOR - Sistema IoT Distribuido      |
+------------------------------------------+
Servidor aguardando conexões na porta 6000...
```

## Passo 4: Executar Gateway

```bash
# Terminal 2: Gateway
dotnet run --project src/Gateway/Gateway.csproj -- 127.0.0.1:6000 ./sensores.csv localhost 5672
```

**Output esperado:**
```
+---------------------------------------------------------------+
|       GATEWAY - Sistema IoT Distribuido (RabbitMQ)           |
+---------------------------------------------------------------+

Configuracao Inicial:
  Servidor remoto: 127.0.0.1:6000
  Ficheiro CSV de sensores: ./sensores.csv
  RabbitMQ: localhost:5672
  RPC Pre-Processamento: http://127.0.0.1:5001

[timestamp] [RabbitMQ] A conectar a RabbitMQ em localhost:5672...
[timestamp] [RabbitMQ] Conectado a RabbitMQ com sucesso!
[timestamp] [RabbitMQ] Exchanges declarados com sucesso
[timestamp] [RabbitMQ] Consumidores iniciados
```

### Verificação
- Gateway iniciada com sucesso
- Conectada a RabbitMQ
- Pronta para receber mensagens

## Passo 5: Executar Sensor

```bash
# Terminal 3: Sensor-01
dotnet run --project src/Sensor/Sensor.csproj -- sensor-01 localhost 5672
```

**Output esperado:**
```
+--------------------------------------------------------------+
|     SENSOR - Sistema IoT Distribuido (com RabbitMQ)        |
+--------------------------------------------------------------+

RabbitMQ Host: localhost:5672
Sensor ID: sensor-01

[HH:mm:ss] A conectar a RabbitMQ em localhost:5672...
[HH:mm:ss] Conectado a RabbitMQ com sucesso!
[HH:mm:ss] Exchanges declarados com sucesso
[HH:mm:ss] A publicar mensagem de registo...
[HH:mm:ss] Registo publicado com sucesso

==========================================================
                      MENU PRINCIPAL                      
==========================================================

Opções disponíveis:
  [1] Enviar medição de Temperatura
  [2] Enviar medição de Humidade
  [3] Enviar medição de Qualidade do Ar
  ...
  [0] Sair
```

## Passo 6: Testar Envio de Dados

### 6.1 Via Menu Interativo

```
Escolha uma opção: 1
Introduza o valor de Temperatura: 22.5
[HH:mm:ss] Medição de temperatura publicada com sucesso.
```

**Output no Gateway:**
```
[timestamp] [PROCESSAMENTO] Tipo: DATA, Sensor: sensor-01
[timestamp] [DATA] Sensor: sensor-01, Tipo: temperatura, Valor: 22.5
[timestamp] [RPC] Uniformizar: sensor-01/temperatura 22.5 -> 22.5 Celsius
[timestamp] [RPC] Validar: sensor-01/temperatura - VALIDO
[timestamp] ✓ Mensagem DATA de sensor-01 encaminhada com sucesso
```

### 6.2 Múltiplas Leituras

```
Escolha uma opção: 2
Introduza o valor de Humidade: 65.3
[HH:mm:ss] Medição de humidade publicada com sucesso.

Escolha uma opção: 3
Introduza o valor de Qualidade do Ar: 50
[HH:mm:ss] Medição de qualidade_ar publicada com sucesso.
```

## Passo 7: Monitorar no RabbitMQ Management

1. Abrir http://localhost:15672
2. Login: guest / guest
3. Abas a explorar:
   - **Connections**: Ver conexões de Sensor e Gateway
   - **Channels**: Ver canais AMQP abertos
   - **Exchanges**: Ver `sensor-measurements` e `sensor-control`
   - **Queues**: Ver `gateway-measurements-*` e `gateway-control-*`
   - **Messages**: Ver mensagens sendo processadas

## Teste de Stress

### Múltiplos Sensores

```bash
# Terminal 4: Sensor-02
dotnet run --project src/Sensor/Sensor.csproj -- sensor-02 localhost 5672

# Enviar vários dados rapidamente
```

### Verificação de Throughput
1. No Management UI, ir para **Queues**
2. Ver taxa de mensagens entrando/saindo
3. Confirmar que o Gateway está processando tudo

## Testes de Validação

### Teste 1: Dados Válidos
- ✅ Sensor envia temperatura 22.5
- ✅ Gateway recebe e processa
- ✅ Servidor recebe mensagem
- ✅ No RabbitMQ Management vê fila vazia (processado)

### Teste 2: Dados Inválidos
- Sensor envia temperatura -500 (inválido)
- ✅ Gateway processa RPC Validar
- ✅ Mensagem é rejeitada
- ✅ Log mostra REJEITADO

### Teste 3: Heartbeat
- ✅ Sensor envia heartbeat a cada 10 segundos
- ✅ Gateway recebe e atualiza LastSync
- ✅ CSV atualizado com timestamp

### Teste 4: Timeout
- Parar Sensor (Ctrl+C)
- Esperar 60 segundos
- ✅ Gateway marca sensor como "manutencao"
- ✅ CSV atualizado

### Teste 5: Reconexão
- Parar RabbitMQ: `docker-compose down`
- Observar logs de erro
- Iniciar RabbitMQ: `docker-compose up -d`
- ✅ Sensores e Gateway reconectam automaticamente

## Cleanup

```bash
# Parar todos os processos (Ctrl+C em cada terminal)

# Parar e remover containers
docker-compose down

# Limpar volume (opcional)
docker-compose down -v

# Limpar binários (opcional)
dotnet clean SharedProtocol.sln
```

## Troubleshooting

### Erro: "Connection refused"
- RabbitMQ não está rodando
- Solução: `docker-compose up -d`

### Erro: "Queue not found"
- Queues não foram criadas
- Solução: Reiniciar Gateway (cria automaticamente)

### Erro: "Channel closed"
- Conexão caiu durante operação
- Solução: Verificar logs de RabbitMQ (`docker logs rabbitmq-sensor-gateway`)

### Sensor não recebe registro
- Confirmar sensor existe em sensores.csv
- Confirmar Gateway está rodando
- Verificar RabbitMQ Management

### Performance lenta
- Verificar carga de RabbitMQ (Management UI)
- Confirmar RPC pré-processamento está respondendo
- Ver logs em gateway.log

## Logs Importantes

```bash
# Gateway log
tail -f gateway.log

# Docker logs
docker-compose logs -f rabbitmq
docker-compose logs -f sensor-gateway-gateway

# System
Get-EventLog -LogName Application -Source Docker
```

## Sucesso Esperado

✅ Sensor conecta a RabbitMQ  
✅ Sensor publica mensagens  
✅ Gateway subscreve e recebe  
✅ Gateway encaminha ao Servidor  
✅ Todos os componentes comunicam corretamente  
✅ Routing funciona por tipo de dado e zona  
