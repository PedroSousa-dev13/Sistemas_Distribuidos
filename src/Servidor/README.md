# Servidor - Fase 3

## Visão Geral

O Servidor é o componente central do sistema que recebe dados ambientais das Gateways e os persiste em ficheiros.

**Porta padrão:** 6000

## Características Implementadas

### ? 3.1 Configuração Inicial
- Aceita porto de escuta como argumento de linha de comandos
- Cria diretório `dados/` automaticamente se não existir

### ? 3.2 Atendimento de Gateways (Concorrência)
- `TcpListener` escuta em porta configurável
- Cada ligação de gateway é tratada em thread dedicada
- Suporta múltiplas gateways em simultâneo
- Contador de gateways conectadas

### ? 3.3 Receção e Parsing de Mensagens
- Recebe mensagens tipo `DATA` das gateways
- Extrai: `sensor_id`, `tipo_dado`, `valor`, `timestamp`
- Valida formato das mensagens (JSON)
- Envia `ERROR` para mensagens malformadas
- Envia `DATA_ACK` após persistência bem-sucedida

### ? 3.4 Persistência em Ficheiros por Tipo de Dado
Cria e mantém ficheiros para cada tipo de dado:
- `temperatura.txt`
- `humidade.txt`
- `qualidade_ar.txt`
- `ruido.txt`
- `pm25.txt`
- `pm10.txt`
- `luminosidade.txt`
- `imagem.txt`

**Formato:** `timestamp|sensor_id|valor`

**Thread-safety:** Cada tipo de dado possui um `Mutex` separado
- Escritas do mesmo tipo: sequenciais (bloqueadas)
- Escritas de tipos diferentes: paralelas (não bloqueadas)

### ? 3.5 Tratamento de Erros e Desligação
- Trata exceções de desligação TCP
- Liberta recursos (streams, sockets)
- Desreferencia mutexes mesmo em caso de erro

### ? 3.6 Logging
- Ficheiro `servidor.log` na pasta `dados/`
- Regista: ligações, medições, erros
- Timestamps em formato ISO 8601
- Thread-safe (com lock)

## Uso

### Compilação
```bash
cd src/Servidor
dotnet build
```

### Execução
```bash
dotnet run <portoEscuta>
```

**Exemplo:**
```bash
dotnet run 6000
```

## Estrutura do Código

### Program.cs
Contém a lógica principal:
- `Main(string[] args)` - Ponto de entrada, inicia listener TCP
- `HandleGateway(TcpClient)` - Thread worker para cada gateway
- `ProcessarDATA(Mensagem, StreamWriter)` - Processa mensagens DATA
- `PersistirMedicao(...)` - Persiste dado com thread-safety
- `InitializeFileMutexes()` - Cria mutexes para cada tipo
- `Log(string)` - Regista em ficheiro de log

### ServidorMonitor.cs
Classe auxiliar para operações do servidor:
- Centraliza I/O e gestão de mutexes
- Métodos thread-safe para persistência
- Facilita testes e reutilização

## Teste

### 1. Teste Manual com Gateway + Sensor

**Terminal 1 - Servidor:**
```bash
cd src/Servidor
dotnet run 6000
```

**Terminal 2 - Gateway:**
```bash
cd src/Gateway
dotnet run 5000 localhost:6000 sensores.csv
```

**Terminal 3 - Sensor:**
```bash
cd src/Sensor
dotnet run localhost 5000 SENSOR_001
```

No menu do sensor, envie algumas medições. Verifique em `src/Servidor/dados/`:
- `temperatura.txt` com registos
- `servidor.log` com atividade

### 2. Teste de Concorrência

Lance múltiplos sensores em simultâneo:
```bash
# Terminal 3
dotnet run localhost 5000 SENSOR_001 &
dotnet run localhost 5000 SENSOR_002 &
dotnet run localhost 5000 SENSOR_003 &
```

Verifique se as mensagens chegam sem erro no log.

### 3. Teste de Desligação

Durante a atividade, feche a gateway (Ctrl+C). Verifique que:
- Servidor regista desligação no log
- Contador de gateways diminui
- Sem crash ou deadlock

## Fluxo de Dados

```
Sensor (DATA) 
    ? TCP:5000
Gateway (DATA)
    ? TCP:6000 (fila de processamento)
Servidor (Handler Thread)
    ? PersistirMedicao() com Mutex por tipo
Ficheiro (.txt)
    ? Log do servidor (servidor.log)
```

## Requisitos

- .NET 9.0
- Protocolo conforme `PROTOCOLO.md`
- Gateway rodando no mesmo ambiente
- Sensor ligado à gateway para testes

## Próximos Passos (Opcional)

- [ ] Adicionar suporte a Base de Dados (SQLite/SQL Server)
- [ ] Implementar query de histórico de medições
- [ ] Dashboard web com dados em tempo real
- [ ] Compressão de ficheiros de dados antigos
