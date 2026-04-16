# DataStreamClient - Cliente de Stream de Dados

Cliente IoT que lê dados de um ficheiro CSV e faz streaming contínuo para a Gateway, simulando sensores reais com dados consistentes e timing realista.

## Propósito

- **Simular múltiplos sensores** simultaneamente com dados pré-definidos
- **Testes de carga** - validar como a Gateway e Servidor lidam com múltiplas fontes de dados
- **Dados determinísticos** - reproduzível para testes e debugging
- **Integração com protocolo** - usa as mesmas mensagens `DATA` que sensores reais

## Características

✅ Lê ficheiros CSV estruturados
✅ Suporta múltiplos sensores num único ficheiro
✅ Respeita timing relativo entre medições
✅ Reutiliza `SensorClient` do Sensor
✅ Execução concorrente (um thread por sensor)
✅ Logging detalhado

## Compilação

```bash
dotnet build src/DataStreamClient/DataStreamClient.csproj -c Debug
```

Ou usar a task:
```
build-datastream
```

## Execução

```bash
dotnet run --project src/DataStreamClient/DataStreamClient.csproj -- \
    <IP_GATEWAY> <PORTO_GATEWAY> <CAMINHO_CSV>
```

### Exemplo

```bash
dotnet run --project src/DataStreamClient/DataStreamClient.csproj -- \
    127.0.0.1 5000 ./dados/stream_dados.csv
```

Ou usar a task:
```
run-datastream-local
```

## Formato CSV

Ficheiro de entrada deve seguir este formato:

```csv
timestamp,sensor_id,zona,tipo_dado,valor
2026-04-16T08:00:00.000Z,sensor-01,Sala_A,temperatura,22.5
2026-04-16T08:00:05.000Z,sensor-01,Sala_A,humidade,45.2
2026-04-16T08:01:00.000Z,sensor-02,Sala_B,temperatura,21.0
```

### Campos

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `timestamp` | ISO 8601 | Hora da medição (UTC) |
| `sensor_id` | string | ID do sensor (ex: `sensor-01`) |
| `zona` | string | Localização do sensor (contextual) |
| `tipo_dado` | string | Tipo de medição (`temperatura`, `humidade`, `qualidade_ar`, etc.) |
| `valor` | número\|string | Valor da medição |

## Fluxo de Funcionamento

```
1. Carregar ficheiro CSV
   ↓
2. Parsear e validar registos
   ↓
3. Agrupar por sensor_id
   ↓
4. Para cada sensor (em paralelo):
   ├─ Iniciar SensorClient
   ├─ Conectar à Gateway
   ├─ Registar com tipos de dados
   ├─ Enviar medições com delays realistas
   └─ Finalizar conexão
```

## Timing

O cliente mantém o timing **relativo** entre medições:

- Se há 5 segundos entre duas medições no CSV, o cliente aguarda 5 segundos
- Delays maiores que 60 segundos são ignorados (evita esperas excessivas)
- Timing absoluto não é garantido (depends da máquina)

## Exemplo Completo

### 1. Iniciar Gateway

```bash
# Terminal 1
dotnet run --project src/Gateway/Gateway.csproj -- \
    5000 127.0.0.1:6000 ./sensores.csv
```

### 2. Iniciar DataStreamClient

```bash
# Terminal 2
dotnet run --project src/DataStreamClient/DataStreamClient.csproj -- \
    127.0.0.1 5000 ./dados/stream_dados.csv
```

### 3. (Opcional) Iniciar Sensor Manual

```bash
# Terminal 3
dotnet run --project src/Sensor/Sensor.csproj -- \
    127.0.0.1 5000 sensor-03
```

Resultado: A Gateway recebe dados de múltiplas fontes simultaneamente!

## Tratamento de Erros

- **Ficheiro não encontrado** - mensagem clara com path
- **CSV inválido** - aviso por linha, continua com restantes
- **Ligação recusada** - trata como erro de inicialização
- **Sensor não registado** - mensagem de erro detalhada

## Exemplos de Dados

Veja [dados/stream_dados.csv](../../dados/stream_dados.csv) para exemplo completo.

## Notas de Implementação

- Usa `DataStreamReader` para parsear CSV robustamente
- Cada sensor funciona num thread separado (`Task`)
- Reutiliza `SensorClient.EnviarMedicaoAsync()` existente
- Mantém sincronização de heartbeat transparente
