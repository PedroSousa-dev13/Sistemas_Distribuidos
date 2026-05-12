# Protocolo de Comunicação - Sistema de Monitorização Ambiental Urbana

## Versão 1.0 - Fase 0

---

## 1. Visão Geral

Este documento define o protocolo de comunicação partilhado entre os três componentes do sistema de monitorização ambiental urbana:

- **Sensor** - Dispositivos que capturam dados ambientais
- **Gateway** - Intermediário que agrega e encaminha dados de múltiplos sensores
- **Servidor** - Sistema central que persiste e processa dados

O protocolo usa TCP/IP como camada de transporte e JSON como formato de serialização de mensagens.

---

## 2. Estrutura Base da Mensagem

Todas as mensagens trocadas entre componentes seguem a mesma estrutura base:

```json
{
  "tipo": "string",
  "sensor_id": "string",
  "payload": { },
  "timestamp": "string (ISO 8601)"
}
```

No JSON transmitido, as chaves são `tipo`, `sensor_id`, `payload` e `timestamp`, em linha com a biblioteca C# de referência do projeto.

### Campos

| Campo | Tipo | Obrigatório | Descrição |
|-------|------|-------------|-----------|
| `tipo` | string | Sim | Tipo da mensagem (ver secção 3) |
| `sensor_id` | string | Condicional | ID do sensor (obrigatório para REGISTER, DATA, HEARTBEAT) |
| `payload` | object | Não | Dados específicos do tipo de mensagem |
| `timestamp` | string | Sim | Marca temporal em formato ISO 8601 (UTC) |

### Exemplo de Timestamp ISO 8601

```
2024-01-15T10:30:00.000Z
```

---

## 3. Tipos de Mensagem

O protocolo define 8 tipos de mensagem:

| Tipo | Direção | Descrição |
|------|---------|-----------|
| `REGISTER` | Sensor → Gateway | Pedido de registo de sensor |
| `REGISTER_OK` | Gateway → Sensor | Confirmação de registo bem-sucedido |
| `REGISTER_ERR` | Gateway → Sensor | Rejeição de registo com código de erro |
| `DATA` | Sensor → Gateway → Servidor | Transmissão de dados ambientais |
| `DATA_ACK` | Gateway → Sensor / Servidor → Gateway | Confirmação de receção de dados |
| `HEARTBEAT` | Sensor → Gateway | Sinal de vida do sensor |
| `HEARTBEAT_ACK` | Gateway → Sensor | Confirmação de receção de heartbeat |
| `ERROR` | Qualquer direção | Notificação de erro |

---

## 4. Especificação Detalhada das Mensagens

### 4.1 REGISTER

**Propósito:** Sensor solicita registo na Gateway

**Direção:** Sensor → Gateway

**Estrutura:**
```json
{
  "tipo": "REGISTER",
  "sensor_id": "SENSOR_001",
  "payload": {
    "tipos_dados": ["temperatura", "humidade", "qualidade_ar"]
  },
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

**Campos do Payload:**
- `tipos_dados` (array de strings) - Lista de tipos de dados que o sensor suporta

**Resposta Esperada:** `REGISTER_OK` ou `REGISTER_ERR`

---

### 4.2 REGISTER_OK

**Propósito:** Gateway confirma registo bem-sucedido

**Direção:** Gateway → Sensor

**Estrutura:**
```json
{
  "tipo": "REGISTER_OK",
  "sensor_id": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:30:01.000Z"
}
```

**Campos do Payload:** Nenhum (payload vazio)

---

### 4.3 REGISTER_ERR

**Propósito:** Gateway rejeita registo com motivo

**Direção:** Gateway → Sensor

**Estrutura:**
```json
{
  "tipo": "REGISTER_ERR",
  "sensor_id": "SENSOR_001",
  "payload": {
    "error_code": "SENSOR_NOT_FOUND",
    "description": "Sensor não encontrado no sistema"
  },
  "timestamp": "2024-01-15T10:30:01.000Z"
}
```

**Campos do Payload:**
- `error_code` (string) - Código de erro (ver secção 6)
- `description` (string) - Descrição legível do erro

---

### 4.4 DATA

**Propósito:** Transmissão de dados ambientais

**Direção:** Sensor → Gateway → Servidor

**Estrutura:**
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

**Campos do Payload:**
- `tipo_dado` (string) - Tipo de dado ambiental (ver secção 5)
- `valor` (any) - Valor da medição (tipo depende do tipo_dado)

**Resposta Esperada:** `DATA_ACK`

---

### 4.5 DATA_ACK

**Propósito:** Confirmação de receção de dados

**Direção:** Gateway → Sensor ou Servidor → Gateway

**Estrutura:**
```json
{
  "tipo": "DATA_ACK",
  "sensor_id": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:35:01.000Z"
}
```

**Campos do Payload:** Nenhum (payload vazio)

---

### 4.6 HEARTBEAT

**Propósito:** Sinal de vida do sensor

**Direção:** Sensor → Gateway

**Estrutura:**
```json
{
  "tipo": "HEARTBEAT",
  "sensor_id": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:40:00.000Z"
}
```

**Campos do Payload:** Nenhum (payload vazio)

**Frequência:** A cada 20 segundos

**Resposta Esperada:** `HEARTBEAT_ACK`

---

### 4.7 HEARTBEAT_ACK

**Propósito:** Confirmação de receção de heartbeat

**Direção:** Gateway → Sensor

**Estrutura:**
```json
{
  "tipo": "HEARTBEAT_ACK",
  "sensor_id": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:40:01.000Z"
}
```

**Campos do Payload:** Nenhum (payload vazio)

---

### 4.8 ERROR

**Propósito:** Notificação de erro genérico

**Direção:** Qualquer componente para qualquer componente

**Estrutura:**
```json
{
  "tipo": "ERROR",
  "sensor_id": "SENSOR_001",
  "payload": {
    "error_code": "SERVER_UNAVAILABLE",
    "description": "Servidor não está disponível"
  },
  "timestamp": "2024-01-15T10:45:00.000Z"
}
```

**Campos do Payload:**
- `error_code` (string) - Código de erro (ver secção 6)
- `description` (string) - Descrição legível do erro

---

## 5. Tipos de Dados Ambientais

Os seguintes tipos de dados são suportados no campo `tipo_dado` de mensagens `DATA`:

| Tipo | Descrição | Unidade | Tipo do Valor |
|------|-----------|---------|---------------|
| `temperatura` | Temperatura ambiente | °C | number |
| `humidade` | Humidade relativa | % | number |
| `qualidade_ar` | Índice de qualidade do ar | - | number |
| `ruido` | Nível de ruído | dB | number |
| `pm25` | Partículas PM2.5 | μg/m³ | number |
| `pm10` | Partículas PM10 | μg/m³ | number |
| `luminosidade` | Nível de luminosidade | lux | number |
| `imagem` | Dados de imagem/vídeo | - | string (URL ou base64) |

### Exemplos

**Temperatura:**
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

**Imagem:**
```json
{
  "tipo": "DATA",
  "sensor_id": "SENSOR_CAM_01",
  "payload": {
    "tipo_dado": "imagem",
    "valor": "https://storage.example.com/images/cam01_20240115_103500.jpg"
  },
  "timestamp": "2024-01-15T10:35:00.000Z"
}
```

---

## 6. Códigos de Erro

| Código | Descrição | Usado em |
|--------|-----------|----------|
| `SENSOR_NOT_FOUND` | Sensor não existe no sistema | REGISTER_ERR |
| `SENSOR_INACTIVE` | Sensor existe mas está inativo | REGISTER_ERR, ERROR |
| `SERVER_UNAVAILABLE` | Servidor não está disponível | ERROR |
| `INVALID_FORMAT` | Formato de mensagem inválido | ERROR |
| `INVALID_DATA_TYPE` | Tipo de dado não suportado | ERROR |

---

## 7. Configuração de Portos

| Componente | Porto Padrão | Descrição |
|------------|--------------|-----------|
| Gateway | 5000 | Porto de escuta para ligações de sensores |
| Servidor | 6000 | Porto de escuta para ligações de gateways |

### Exemplo de Ligação

**Sensor conecta à Gateway:**
```
IP: 192.168.1.100
Porto: 5000
```

**Gateway conecta ao Servidor:**
```
IP: 192.168.1.200
Porto: 6000
```

---

## 8. Fluxos de Comunicação

### 8.1 Fluxo de Registo

```
Sensor                    Gateway
  |                          |
  |-------- REGISTER ------->|
  |                          | (valida sensor no CSV)
  |<----- REGISTER_OK -------|
  |                          |
```

**Cenário de Erro:**
```
Sensor                    Gateway
  |                          |
  |-------- REGISTER ------->|
  |                          | (sensor não encontrado)
  |<---- REGISTER_ERR -------|
  |                          |
  | (termina ligação)        |
```

---

### 8.2 Fluxo de Transmissão de Dados

```
Sensor              Gateway              Servidor
  |                    |                     |
  |------ DATA ------->|                     |
  |                    |------ DATA -------->|
  |                    |                     | (persiste)
  |                    |<---- DATA_ACK ------|
  |<--- DATA_ACK ------|                     |
  |                    |                     |
```

---

### 8.3 Fluxo de Heartbeat

```
Sensor                    Gateway
  |                          |
  |------ HEARTBEAT -------->|
  |                          | (atualiza last_sync no CSV)
  |<--- HEARTBEAT_ACK -------|
  |                          |
  | (aguarda 20 segundos)    |
  |                          |
  |------ HEARTBEAT -------->|
  |<--- HEARTBEAT_ACK -------|
  |                          |
```

**Cenário de Timeout:**
```
Sensor                    Gateway
  |                          |
  | (sem mensagens > 60s)    |
  |                          | (watchdog thread)
  |                          | (marca sensor como "manutencao")
  |                          |
```

---

### 8.4 Fluxo de Erro

```
Sensor              Gateway              Servidor
  |                    |                     |
  |------ DATA ------->|                     |
  |                    | (servidor offline)  |
  |                    |  X                  |
  |<----- ERROR -------|                     |
  | (error_code:       |                     |
  |  SERVER_UNAVAILABLE)|                    |
  |                    |                     |
```

---

## 9. Formato de Transmissão

### 9.1 Serialização

As mensagens são serializadas em JSON compacto (sem indentação) antes de serem enviadas via TCP.

**Exemplo:**
```json
{"tipo":"DATA","sensor_id":"SENSOR_001","payload":{"tipo_dado":"temperatura","valor":23.5},"timestamp":"2024-01-15T10:35:00.000Z"}
```

### 9.2 Delimitação de Mensagens

Cada mensagem JSON é terminada com um caractere de nova linha (`\n`) para facilitar a leitura no stream TCP.

**Formato:**
```
[JSON_MESSAGE]\n
```

**Exemplo de Stream:**
```
{"tipo":"REGISTER","sensor_id":"SENSOR_001","payload":{"tipos_dados":["temperatura"]},"timestamp":"2024-01-15T10:30:00.000Z"}\n
{"tipo":"REGISTER_OK","sensor_id":"SENSOR_001","payload":{},"timestamp":"2024-01-15T10:30:01.000Z"}\n
```

### 9.3 Encoding

Todas as mensagens usam codificação **UTF-8**.

---

## 10. Tratamento de Erros

### 10.1 Mensagens Malformadas

Se um componente recebe uma mensagem com JSON inválido:
1. Descartar a mensagem
2. Registar erro no log local
3. Enviar mensagem `ERROR` com código `INVALID_FORMAT` (se possível identificar remetente)
4. Continuar a processar outras mensagens

### 10.2 Campos Obrigatórios em Falta

Se uma mensagem não contém campos obrigatórios:
1. Descartar a mensagem
2. Registar erro no log local
3. Enviar mensagem `ERROR` com código `INVALID_FORMAT`

### 10.3 Timeout de Resposta

Se um componente não recebe resposta esperada dentro do timeout:
- **Sensor:** Registar aviso, pode reenviar ou reportar ao utilizador
- **Gateway:** Registar erro, enviar ERROR ao sensor se aplicável
- **Servidor:** Registar erro no log

**Timeouts Recomendados:**
- Resposta a REGISTER: 5 segundos
- Resposta a DATA: 10 segundos
- Resposta a HEARTBEAT: 5 segundos

---

## 11. Extensibilidade

### 11.1 Adicionar Novos Tipos de Mensagem

Para adicionar um novo tipo de mensagem:

1. Definir nova constante em `TiposMensagem`
2. Adicionar à lista `TiposMensagem.Validos`
3. Criar factory method em `Mensagem` (opcional mas recomendado)
4. Documentar estrutura do payload neste documento
5. Atualizar handlers nos componentes que processam o novo tipo

**Exemplo:**
```csharp
public static class TiposMensagem
{
    // Tipos existentes...
    public const string NOVO_TIPO = "NOVO_TIPO";
    
    public static readonly HashSet<string> Validos = new HashSet<string>
    {
        // Tipos existentes...
        NOVO_TIPO
    };
}
```

### 11.2 Adicionar Novos Tipos de Dados Ambientais

Para adicionar um novo tipo de dado ambiental:

1. Documentar o novo tipo na secção 5 deste documento
2. Especificar unidade e tipo do valor
3. Nenhuma alteração de código necessária (payload é flexível)
4. Atualizar Servidor para persistir o novo tipo (criar novo ficheiro .txt)

---

## 12. Considerações de Segurança

### 12.1 Validação de Entrada

Todos os componentes DEVEM:
- Validar formato JSON antes de processar
- Validar tipos de campos
- Validar valores de campos (ranges, formatos)
- Rejeitar mensagens inválidas sem crashar

### 12.2 Limites de Tamanho

Recomenda-se implementar limites de tamanho de mensagem:
- Tamanho máximo de mensagem: 1 MB
- Tamanho máximo de payload: 512 KB
- Mensagens maiores devem ser rejeitadas com ERROR

### 12.3 Rate Limiting

Componentes podem implementar rate limiting para prevenir sobrecarga:
- Gateway: Limitar mensagens por sensor por segundo
- Servidor: Limitar mensagens por gateway por segundo

---

## 13. Exemplo Completo de Sessão

```
# Sensor inicia e conecta à Gateway (192.168.1.100:5000)

→ {"tipo":"REGISTER","sensor_id":"SENSOR_001","payload":{"tipos_dados":["temperatura","humidade"]},"timestamp":"2024-01-15T10:30:00.000Z"}

← {"tipo":"REGISTER_OK","sensor_id":"SENSOR_001","payload":{},"timestamp":"2024-01-15T10:30:01.000Z"}

# Sensor envia primeira medição

→ {"tipo":"DATA","sensor_id":"SENSOR_001","payload":{"tipo_dado":"temperatura","valor":23.5},"timestamp":"2024-01-15T10:35:00.000Z"}

← {"tipo":"DATA_ACK","sensor_id":"SENSOR_001","payload":{},"timestamp":"2024-01-15T10:35:01.000Z"}

# Sensor envia segunda medição

→ {"tipo":"DATA","sensor_id":"SENSOR_001","payload":{"tipo_dado":"humidade","valor":65.2},"timestamp":"2024-01-15T10:36:00.000Z"}

← {"tipo":"DATA_ACK","sensor_id":"SENSOR_001","payload":{},"timestamp":"2024-01-15T10:36:01.000Z"}

# Sensor envia heartbeat (20 segundos depois do último)

→ {"tipo":"HEARTBEAT","sensor_id":"SENSOR_001","payload":{},"timestamp":"2024-01-15T10:50:00.000Z"}

← {"tipo":"HEARTBEAT_ACK","sensor_id":"SENSOR_001","payload":{},"timestamp":"2024-01-15T10:50:01.000Z"}

# Sensor desliga gracefully (fecha ligação TCP)
```

---

## 14. Referências de Implementação

### 14.1 Classe Mensagem (C#)

Ver ficheiro: `.kiro/specs/shared-protocol-phase0/design.md`

### 14.2 Serialização

Biblioteca: `System.Text.Json` (nativo .NET)

### 14.3 Testes

Framework de testes: xUnit + FsCheck (property-based testing)

---

## 15. Histórico de Versões

| Versão | Data | Alterações |
|--------|------|------------|
| 1.0 | 2024-01-15 | Versão inicial - Fase 0 |

---

## 16. Contacto e Suporte

Para questões sobre o protocolo, consultar:
- Documento de Design: `.kiro/specs/shared-protocol-phase0/design.md`
- Documento de Requisitos: `.kiro/specs/shared-protocol-phase0/requirements.md`

