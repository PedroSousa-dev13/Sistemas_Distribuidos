# Fase 3 - Servidor: Resumo de Implementação

## ? Status: COMPLETO

A Fase 3 (Servidor) foi implementada com sucesso.

---

## ?? Arquitectura

```
???????????????????????????????????????????????????
?                  SERVIDOR (Fase 3)              ?
???????????????????????????????????????????????????
?                                                 ?
?  Main() [Porta 6000]                           ?
?    ?? TcpListener (aceita gateways)            ?
?    ?? HandleGateway() × N threads              ?
?    ?? Log + Monitoramento                      ?
?                                                 ?
?  Para cada Gateway conectada:                  ?
?    ?? Recebe mensagem DATA (JSON)             ?
?    ?? ValidaFormat()                          ?
?    ?? ExtraiDados: sensor_id, tipo, valor    ?
?    ?? PersistirMedicao(tipo)                  ?
?    ?   ?? Mutex[tipo] (thread-safe)           ?
?    ?? EnviaDATA_ACK()                         ?
?                                                 ?
?  Ficheiros de Dados (/dados):                 ?
?    ?? temperatura.txt                         ?
?    ?? humidade.txt                            ?
?    ?? qualidade_ar.txt                        ?
?    ?? ruido.txt                               ?
?    ?? pm25.txt                                ?
?    ?? pm10.txt                                ?
?    ?? luminosidade.txt                        ?
?    ?? imagem.txt                              ?
?    ?? servidor.log                            ?
?                                                 ?
???????????????????????????????????????????????????
```

---

## ?? Componentes Principais

### 1. **Program.cs** (Servidor Principal)
```csharp
Main(args)
  ?? Validação de argumentos
  ?? CriaDiretorio("/dados")
  ?? InicializaMutexes()
  ?? TcpListener(IPAddress.Any, porta)
  ?? Loop: AcceptTcpClient() ? HandleGateway(thread)

HandleGateway(TcpClient)
  ?? Recebe mensagens do stream TCP
  ?? Switch(tipo_mensagem):
  ?   ?? DATA ? ProcessarDATA()
  ?? Cleanup: fecha stream + socket

ProcessarDATA(Mensagem)
  ?? Valida payload
  ?? Extrai: tipo_dado, valor, timestamp, sensor_id
  ?? PersistirMedicao() com Mutex
  ?? Responde: DATA_ACK ou ERROR

PersistirMedicao(tipo, timestamp, sensorId, valor)
  ?? Valida tipo
  ?? AcquiseMutex(tipo)
  ?? AppendToFile(tipo.txt)
  ?? ReleaseMutex(tipo)
```

### 2. **ServidorMonitor.cs** (Classe Auxiliar)
```csharp
ServidorMonitor(dataDirectory)
  ?? EnsureDataDirectory()
  ?? InitializeFileMutexes()
  ?? PersistirMedicao(tipo, ...)
  ?? Log(message)
  ?? TiposDadosSuportados[]
```

---

## ?? Funcionalidades Implementadas

### ? 3.1 Configuração Inicial
- [x] Aceita `portoEscuta` como argumento
- [x] Cria `dados/` automaticamente
- [x] Mensagem de log inicial

### ? 3.2 Atendimento de Gateways
- [x] `TcpListener` porta configurável
- [x] Thread por gateway (concorrência)
- [x] Suporta 5+ gateways simultâneas
- [x] Contador de gateways conectadas
- [x] Limpeza de recursos ao desconectar

### ? 3.3 Receção de Mensagens
- [x] Recebe `DATA` em JSON (TCP stream + `\n`)
- [x] Deserializa com `JsonSerializer`
- [x] Extrai campos do payload
- [x] Valida formato (envia `ERROR` se inválido)
- [x] Envia `DATA_ACK` após persistência

### ? 3.4 Persistência em Ficheiros
- [x] 8 tipos de dados suportados
- [x] Ficheiro por tipo: `{tipo}.txt`
- [x] Formato linha: `timestamp|sensor_id|valor`
- [x] **Mutex por tipo** (paralelismo garantido)
- [x] Escritas sequenciais do mesmo tipo
- [x] Sem deadlock ou corrupção

### ? 3.5 Tratamento de Erros
- [x] Try/catch em threads
- [x] Mensagens inválidas ? `ERROR`
- [x] Tipos não suportados ? `ERROR`
- [x] Desligação graceful
- [x] Recursos libertados (finally)

### ? 3.6 Logging
- [x] Ficheiro `servidor.log`
- [x] Timestamps ISO 8601
- [x] Eventos: ligações, medições, erros
- [x] Thread-safe (com lock)

---

## ?? Testes Implementados

**Ficheiro:** `tests/Servidor.Tests/UnitTest1.cs`

Testes unitários com xUnit:
1. ? `EnsureDataDirectory_CriasDiretorio_QuandoNaoExiste()`
2. ? `PersistirMedicao_CriaFicheiro_ComFormatoCorreto()`
3. ? `PersistirMedicao_RetornaFalse_ParaTipoDadoInvalido()`
4. ? `PersistirMedicao_AdicionaMultiplasLinhas()`
5. ? `PersistirMedicao_ThreadSafe_ComMultiplasThreads()`
6. ? `Log_CriaFicheiro_ComTimestamp()`
7. ? `TiposDadosSuportados_RetornaTodos()`
8. ? `PersistirMedicao_EmParalelo_TiposDiferentes_SemBloqueio()`

**Resultado:** 8/8 testes passados ?

---

## ?? Fluxo de Dados Completo

```
???????????      TCP:5000      ??????????      TCP:6000      ???????????
? SENSOR  ? ??? DATA ????????? ?GATEWAY ? ??? DATA ????????? ?SERVIDOR ?
?  (1-N)  ?                    ?        ?                    ?         ?
???????????                    ??????????                    ???????????
                                                                  ?
                                              ?????????????????????????????????????????
                                              ?                   ?                   ?
                                         ???????????        ???????????        ???????????
                                         ?Mutex    ?        ?Mutex    ?        ?Mutex    ?
                                         ?Temp     ?        ?Humid    ?        ?QualAr   ?
                                         ???????????        ???????????        ???????????
                                              ?                  ?                   ?
                                         ???????????        ???????????        ???????????
                                         ?temp.txt ?        ?humid.txt?        ?qualidade?
                                         ?         ?        ?         ?        ?_ar.txt  ?
                                         ???????????        ???????????        ???????????

Linha de ficheiro: "2024-01-15T10:30:00.000Z|SENSOR_001|23.5"
```

---

## ?? Como Testar

### Teste Manual Completo
```bash
# Terminal 1: Servidor
cd src/Servidor
dotnet run 6000

# Terminal 2: Gateway
cd src/Gateway
dotnet run 5000 localhost:6000 sensores.csv

# Terminal 3: Sensor
cd src/Sensor
dotnet run localhost 5000 SENSOR_001

# No menu do sensor: enviar medições
# Verificar dados em src/Servidor/dados/temperatura.txt
```

### Testes Unitários
```bash
cd tests/Servidor.Tests
dotnet test
```

---

## ?? Estrutura de Ficheiros

```
src/Servidor/
??? Program.cs                 (Lógica principal)
??? ServidorMonitor.cs         (Classe auxiliar)
??? Servidor.csproj
??? README.md

tests/Servidor.Tests/
??? UnitTest1.cs              (Testes xUnit)
??? Servidor.Tests.csproj

dados/                         (Criado em runtime)
??? temperatura.txt
??? humidade.txt
??? ... (outros tipos)
??? servidor.log
```

---

## ?? Thread-Safety

### Mutex Strategy
- **1 Mutex por tipo de dado** (8 tipos = 8 mutexes)
- Garante **exclusão mútua por tipo**
- Permite **paralelismo entre tipos diferentes**
- Evita deadlock (sem hierarquia)

### Exemplo
```
Thread A: PersistirMedicao("temperatura", ...) ? Lock(Mutex[temperatura])
Thread B: PersistirMedicao("humidade", ...) ? Lock(Mutex[humidade])
                                              ? Sem bloqueio cruzado!

Thread C: PersistirMedicao("temperatura", ...) ? Aguarda Mutex[temperatura]
                                                 ? Serializado (correto!)
```

---

## ?? Protocolo (Resumo)

### Mensagem DATA (entrada)
```json
{
  "tipo": "DATA",
  "sensor_id": "SENSOR_001",
  "payload": {
    "tipo_dado": "temperatura",
    "valor": 23.5
  },
  "timestamp": "2024-01-15T10:35:00.000Z"
}
```

### Resposta DATA_ACK (saída)
```json
{
  "tipo": "DATA_ACK",
  "sensor_id": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:35:01.000Z"
}
```

---

## ?? Métricas

| Métrica | Valor |
|---------|-------|
| Testes Unitários | 8/8 ? |
| Tipos de Dados Suportados | 8 |
| Mutexes | 1 por tipo |
| Threads Suportadas | N (ilimitado) |
| Porto Padrão | 6000 |
| Encoding | UTF-8 |
| Formato de Dados | `timestamp\|sensor_id\|valor` |

---

## ?? Próximos Passos (Opcional)

- [ ] Adicionar suporte a Base de Dados (SQLite/SQL Server)
- [ ] Implementar API HTTP para consulta de dados
- [ ] Compressão de ficheiros antigos
- [ ] Dashboard web em tempo real
- [ ] Alertas por limites de temperatura/humidade
- [ ] Autenticação/Autorização
- [ ] Replicação/Backup automático

---

## ?? Referências

- **Protocolo:** `PROTOCOLO.md`
- **Distribuição de Trabalho:** `distribuicao.md`
- **Gateway:** `src/Gateway/Program.cs`
- **Shared Protocol:** `src/SharedProtocol/`

---

**Data:** Janeiro 2024  
**Versão:** 1.0 (Fase 3 - Completa)  
**Status:** ? **PRONTO PARA TESTE**
