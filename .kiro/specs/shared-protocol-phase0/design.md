# Design Document: Shared Protocol Phase 0

## Overview

Este documento especifica o design técnico do protocolo de comunicação partilhado para o sistema de monitorização ambiental urbana. O protocolo define uma estrutura de mensagens comum implementada em C# que permite comunicação confiável entre três componentes distribuídos: Sensor, Gateway e Servidor.

O design foca-se em:
- Estrutura de mensagens simples e extensível
- Serialização/deserialização eficiente usando JSON
- Thread-safety para uso concorrente
- Validação robusta de mensagens
- Documentação clara do protocolo

A implementação usa System.Text.Json (nativo do .NET) para serialização, proporcionando melhor performance e menor overhead comparado com bibliotecas externas.

## Architecture

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Protocol Layer                            │
│                                                               │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────┐  │
│  │   Mensagem   │───▶│  Serializer  │───▶│     JSON     │  │
│  │    Class     │    │              │    │    String    │  │
│  └──────────────┘    └──────────────┘    └──────────────┘  │
│         ▲                                         │          │
│         │                                         ▼          │
│         │            ┌──────────────┐    ┌──────────────┐  │
│         └────────────│ Deserializer │◀───│  TCP Stream  │  │
│                      │              │    │              │  │
│                      └──────────────┘    └──────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

### Component Responsibilities

**Mensagem Class**
- Encapsula todos os campos da mensagem (tipo, sensor_id, payload, timestamp)
- Valida dados na construção
- Fornece acesso thread-safe aos campos (imutável)

**Serializer**
- Converte objetos Mensagem para JSON string
- Usa System.Text.Json.JsonSerializer
- Thread-safe (stateless)

**Deserializer**
- Converte JSON string para objetos Mensagem
- Valida formato e campos obrigatórios
- Lança exceções descritivas para dados inválidos
- Thread-safe (stateless)

### Design Decisions

**1. JSON vs Custom Format**
- Escolha: JSON (System.Text.Json)
- Razão: Formato standard, legível, suportado nativamente, extensível, ferramentas de debug disponíveis

**2. Immutability**
- Escolha: Mensagem como classe imutável (readonly fields, init-only properties)
- Razão: Thread-safety automática, previne modificações acidentais, facilita debugging

**3. Validation Strategy**
- Escolha: Validação no construtor + validação na deserialização
- Razão: Fail-fast, garante que objetos Mensagem são sempre válidos

**4. Payload as Dictionary**
- Escolha: Dictionary<string, object> para payload
- Razão: Flexibilidade para diferentes tipos de mensagem, extensível sem quebrar compatibilidade

## Components and Interfaces

### Mensagem Class

```csharp
public class Mensagem
{
    // Campos principais
    public string Tipo { get; init; }
    public string SensorId { get; init; }
    public Dictionary<string, object> Payload { get; init; }
    public string Timestamp { get; init; }

    // Construtor com validação
    public Mensagem(string tipo, string sensorId, Dictionary<string, object> payload, string timestamp)
    {
        // Validação de tipo
        if (string.IsNullOrWhiteSpace(tipo))
            throw new ArgumentException("Tipo não pode ser nulo ou vazio", nameof(tipo));
        
        if (!TiposMensagem.Validos.Contains(tipo))
            throw new ArgumentException($"Tipo '{tipo}' não é válido", nameof(tipo));

        // Validação de sensor_id para tipos que requerem
        if (TiposMensagem.RequerSensorId(tipo) && string.IsNullOrWhiteSpace(sensorId))
            throw new ArgumentException($"SensorId é obrigatório para mensagens do tipo '{tipo}'", nameof(sensorId));

        // Validação de timestamp
        if (!DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out _))
            throw new ArgumentException("Timestamp deve estar em formato ISO 8601 válido", nameof(timestamp));

        Tipo = tipo;
        SensorId = sensorId ?? string.Empty;
        Payload = payload ?? new Dictionary<string, object>();
        Timestamp = timestamp;
    }

    // Factory methods para tipos específicos
    public static Mensagem CriarRegister(string sensorId, List<string> tiposDados)
    {
        var payload = new Dictionary<string, object>
        {
            ["tipos_dados"] = tiposDados
        };
        return new Mensagem(TiposMensagem.REGISTER, sensorId, payload, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarRegisterOk(string sensorId)
    {
        return new Mensagem(TiposMensagem.REGISTER_OK, sensorId, null, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarRegisterErr(string sensorId, string errorCode, string description)
    {
        var payload = new Dictionary<string, object>
        {
            ["error_code"] = errorCode,
            ["description"] = description
        };
        return new Mensagem(TiposMensagem.REGISTER_ERR, sensorId, payload, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarData(string sensorId, string tipoDado, object valor)
    {
        var payload = new Dictionary<string, object>
        {
            ["tipo_dado"] = tipoDado,
            ["valor"] = valor
        };
        return new Mensagem(TiposMensagem.DATA, sensorId, payload, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarDataAck(string sensorId)
    {
        return new Mensagem(TiposMensagem.DATA_ACK, sensorId, null, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarHeartbeat(string sensorId)
    {
        return new Mensagem(TiposMensagem.HEARTBEAT, sensorId, null, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarHeartbeatAck(string sensorId)
    {
        return new Mensagem(TiposMensagem.HEARTBEAT_ACK, sensorId, null, DateTime.UtcNow.ToString("o"));
    }

    public static Mensagem CriarError(string sensorId, string errorCode, string description)
    {
        var payload = new Dictionary<string, object>
        {
            ["error_code"] = errorCode,
            ["description"] = description
        };
        return new Mensagem(TiposMensagem.ERROR, sensorId, payload, DateTime.UtcNow.ToString("o"));
    }
}
```

### TiposMensagem Static Class

```csharp
public static class TiposMensagem
{
    public const string REGISTER = "REGISTER";
    public const string REGISTER_OK = "REGISTER_OK";
    public const string REGISTER_ERR = "REGISTER_ERR";
    public const string DATA = "DATA";
    public const string DATA_ACK = "DATA_ACK";
    public const string HEARTBEAT = "HEARTBEAT";
    public const string HEARTBEAT_ACK = "HEARTBEAT_ACK";
    public const string ERROR = "ERROR";

    public static readonly HashSet<string> Validos = new HashSet<string>
    {
        REGISTER, REGISTER_OK, REGISTER_ERR,
        DATA, DATA_ACK,
        HEARTBEAT, HEARTBEAT_ACK,
        ERROR
    };

    public static bool RequerSensorId(string tipo)
    {
        return tipo == DATA || tipo == HEARTBEAT || tipo == REGISTER;
    }
}
```

### CodigosErro Static Class

```csharp
public static class CodigosErro
{
    public const string SENSOR_NOT_FOUND = "SENSOR_NOT_FOUND";
    public const string SENSOR_INACTIVE = "SENSOR_INACTIVE";
    public const string SERVER_UNAVAILABLE = "SERVER_UNAVAILABLE";
    public const string INVALID_FORMAT = "INVALID_FORMAT";
    public const string INVALID_DATA_TYPE = "INVALID_DATA_TYPE";
}
```

### MensagemSerializer Static Class

```csharp
using System.Text.Json;

public static class MensagemSerializer
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serializar(Mensagem mensagem)
    {
        if (mensagem == null)
            throw new ArgumentNullException(nameof(mensagem));

        return JsonSerializer.Serialize(mensagem, Options);
    }

    public static Mensagem Deserializar(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("JSON não pode ser nulo ou vazio", nameof(json));

        try
        {
            var mensagem = JsonSerializer.Deserialize<Mensagem>(json, Options);
            
            if (mensagem == null)
                throw new InvalidOperationException("Deserialização resultou em objeto nulo");

            return mensagem;
        }
        catch (JsonException ex)
        {
            throw new FormatException($"Formato JSON inválido: {ex.Message}", ex);
        }
    }
}
```

### PortosProtocolo Static Class

```csharp
public static class PortosProtocolo
{
    public const int GATEWAY_PORT = 5000;
    public const int SERVER_PORT = 6000;
}
```

## Data Models

### Message Type Structures

Cada tipo de mensagem tem uma estrutura específica de payload:

**REGISTER**
```json
{
  "tipo": "REGISTER",
  "sensorId": "SENSOR_001",
  "payload": {
    "tipos_dados": ["temperatura", "humidade", "qualidade_ar"]
  },
  "timestamp": "2024-01-15T10:30:00.000Z"
}
```

**REGISTER_OK**
```json
{
  "tipo": "REGISTER_OK",
  "sensorId": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:30:01.000Z"
}
```

**REGISTER_ERR**
```json
{
  "tipo": "REGISTER_ERR",
  "sensorId": "SENSOR_001",
  "payload": {
    "error_code": "SENSOR_NOT_FOUND",
    "description": "Sensor não encontrado no sistema"
  },
  "timestamp": "2024-01-15T10:30:01.000Z"
}
```

**DATA**
```json
{
  "tipo": "DATA",
  "sensorId": "SENSOR_001",
  "payload": {
    "tipo_dado": "temperatura",
    "valor": 23.5
  },
  "timestamp": "2024-01-15T10:35:00.000Z"
}
```

**DATA_ACK**
```json
{
  "tipo": "DATA_ACK",
  "sensorId": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:35:01.000Z"
}
```

**HEARTBEAT**
```json
{
  "tipo": "HEARTBEAT",
  "sensorId": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:40:00.000Z"
}
```

**HEARTBEAT_ACK**
```json
{
  "tipo": "HEARTBEAT_ACK",
  "sensorId": "SENSOR_001",
  "payload": {},
  "timestamp": "2024-01-15T10:40:01.000Z"
}
```

**ERROR**
```json
{
  "tipo": "ERROR",
  "sensorId": "SENSOR_001",
  "payload": {
    "error_code": "SERVER_UNAVAILABLE",
    "description": "Servidor não está disponível"
  },
  "timestamp": "2024-01-15T10:45:00.000Z"
}
```

### Supported Sensor Data Types

O protocolo suporta os seguintes tipos de dados de sensores (campo `tipo_dado` em mensagens DATA):

- `temperatura` - Medições de temperatura (valor numérico em °C)
- `humidade` - Medições de humidade (valor numérico em %)
- `qualidade_ar` - Índice de qualidade do ar (valor numérico)
- `ruido` - Nível de ruído (valor numérico em dB)
- `pm25` - Partículas PM2.5 (valor numérico em μg/m³)
- `pm10` - Partículas PM10 (valor numérico em μg/m³)
- `luminosidade` - Nível de luminosidade (valor numérico em lux)
- `imagem` - Dados de imagem/vídeo (string codificada ou URL)

### Thread-Safety Considerations

**Immutability Pattern**
- A classe Mensagem usa `init` properties, tornando-a imutável após construção
- Objetos imutáveis são automaticamente thread-safe para leitura
- Múltiplas threads podem ler a mesma mensagem sem sincronização

**Stateless Serialization**
- MensagemSerializer é uma classe estática sem estado
- Cada chamada a Serializar/Deserializar é independente
- System.Text.Json é thread-safe para operações de serialização/deserialização

**Concurrent Usage Pattern**
```csharp
// Thread 1
var msg1 = Mensagem.CriarData("SENSOR_001", "temperatura", 23.5);
var json1 = MensagemSerializer.Serializar(msg1);

// Thread 2 (simultânea)
var msg2 = Mensagem.CriarHeartbeat("SENSOR_002");
var json2 = MensagemSerializer.Serializar(msg2);

// Ambas as operações são seguras e não interferem entre si
```


## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Serialization Round-Trip Preservation

*For any* valid Mensagem object, serializing it to JSON and then deserializing back should produce an equivalent object where all fields (tipo, sensorId, payload, timestamp) have identical values.

**Validates: Requirements 3.1, 3.4, 4.1, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5**

### Property 2: Invalid JSON Rejection

*For any* string that is not valid JSON or does not conform to the Mensagem structure, attempting to deserialize should throw a FormatException or ArgumentException without crashing the application.

**Validates: Requirements 4.4**

### Property 3: Concurrent Serialization Safety

*For any* collection of valid Mensagem objects, when multiple threads serialize different messages concurrently, each thread should produce a correct JSON string representation of its respective message without interference or corruption.

**Validates: Requirements 6.1**

### Property 4: Concurrent Deserialization Safety

*For any* collection of valid JSON strings representing messages, when multiple threads deserialize different strings concurrently, each thread should produce a correct Mensagem object without interference or corruption.

**Validates: Requirements 6.2**

### Property 5: Null or Empty Tipo Rejection

*For any* attempt to create a Mensagem with a null or empty string as the tipo field, the constructor should throw an ArgumentException with a descriptive error message.

**Validates: Requirements 7.1**

### Property 6: Invalid Tipo Rejection

*For any* string that is not one of the defined message types (REGISTER, REGISTER_OK, REGISTER_ERR, DATA, DATA_ACK, HEARTBEAT, HEARTBEAT_ACK, ERROR), attempting to create a Mensagem with that tipo should throw an ArgumentException.

**Validates: Requirements 7.2**

### Property 7: Conditional SensorId Validation

*For any* Mensagem with tipo equal to "DATA", "HEARTBEAT", or "REGISTER", if the sensorId is null or empty, the constructor should throw an ArgumentException; for other message types, sensorId may be empty without error.

**Validates: Requirements 7.3**

### Property 8: Invalid Timestamp Rejection

*For any* string that is not a valid ISO 8601 timestamp format, attempting to create a Mensagem with that timestamp should throw an ArgumentException.

**Validates: Requirements 7.4**

### Property 9: Unknown Message Type Graceful Handling

*For any* JSON string containing a tipo field with an unknown message type value, the deserializer should either throw a descriptive exception or handle it gracefully without crashing the application.

**Validates: Requirements 10.2**

## Error Handling

### Validation Errors

**Constructor Validation**
- All validation occurs in the Mensagem constructor
- Fail-fast approach: invalid data throws exceptions immediately
- Exceptions include descriptive messages indicating which field failed and why

**Exception Types**
- `ArgumentException` - For null, empty, or invalid field values
- `ArgumentNullException` - For null required parameters
- `FormatException` - For invalid JSON during deserialization

### Serialization Errors

**Serialization Failures**
- Rare in practice since Mensagem objects are pre-validated
- If serialization fails, JsonException is thrown with details
- Caller should catch and log the exception

**Deserialization Failures**
- Invalid JSON format → FormatException with original JsonException as inner exception
- Missing required fields → ArgumentException during Mensagem construction
- Invalid field values → ArgumentException during Mensagem construction

### Error Message Structure

Error messages follow a consistent pattern:

```csharp
// Field validation
throw new ArgumentException("Tipo não pode ser nulo ou vazio", nameof(tipo));
throw new ArgumentException($"Tipo '{tipo}' não é válido", nameof(tipo));
throw new ArgumentException($"SensorId é obrigatório para mensagens do tipo '{tipo}'", nameof(sensorId));
throw new ArgumentException("Timestamp deve estar em formato ISO 8601 válido", nameof(timestamp));

// Deserialization
throw new FormatException($"Formato JSON inválido: {ex.Message}", ex);
```

### Error Recovery Strategies

**At Protocol Level**
- Invalid messages should be logged and discarded
- Send ERROR message back to sender when appropriate
- Continue processing other messages (don't crash)

**At Application Level**
- Sensor: Log error, retry or prompt user
- Gateway: Log error, send ERROR response, continue serving other sensors
- Servidor: Log error, send ERROR response, continue serving other gateways

### Network Error Handling

While not part of the protocol implementation itself, applications using the protocol should handle:

- TCP connection failures
- Timeout waiting for responses
- Partial message reception
- Connection drops mid-transmission

The protocol provides ERROR message type for communicating these issues between components.

## Testing Strategy

### Dual Testing Approach

The protocol implementation requires both unit testing and property-based testing for comprehensive coverage:

**Unit Tests** - Focus on:
- Specific examples of each message type creation
- Edge cases (null payloads, empty strings, boundary values)
- Error conditions (invalid inputs, malformed JSON)
- Structural requirements (field existence, constant values)
- Integration between components

**Property-Based Tests** - Focus on:
- Universal properties that hold for all inputs
- Round-trip serialization across all valid messages
- Concurrent access patterns
- Validation rules across input space
- Comprehensive input coverage through randomization

### Property-Based Testing Configuration

**Library Selection**
- Use **FsCheck** for C# property-based testing
- FsCheck integrates with xUnit, NUnit, and MSTest
- Provides generators for custom types and complex data structures

**Test Configuration**
- Minimum 100 iterations per property test
- Each test tagged with reference to design document property
- Tag format: `// Feature: shared-protocol-phase0, Property {number}: {property_text}`

**Example Property Test Structure**

```csharp
using FsCheck;
using FsCheck.Xunit;

public class MensagemPropertyTests
{
    // Feature: shared-protocol-phase0, Property 1: Serialization Round-Trip Preservation
    [Property(MaxTest = 100)]
    public Property RoundTripPreservesAllFields()
    {
        return Prop.ForAll(
            MensagemGenerators.ValidMensagem(),
            mensagem =>
            {
                var json = MensagemSerializer.Serializar(mensagem);
                var deserialized = MensagemSerializer.Deserializar(json);
                
                return deserialized.Tipo == mensagem.Tipo &&
                       deserialized.SensorId == mensagem.SensorId &&
                       deserialized.Timestamp == mensagem.Timestamp &&
                       PayloadsAreEqual(deserialized.Payload, mensagem.Payload);
            });
    }

    // Feature: shared-protocol-phase0, Property 3: Concurrent Serialization Safety
    [Property(MaxTest = 100)]
    public Property ConcurrentSerializationIsThreadSafe()
    {
        return Prop.ForAll(
            Gen.ListOf(10, MensagemGenerators.ValidMensagem()).ToArbitrary(),
            mensagens =>
            {
                var results = new ConcurrentBag<(Mensagem, string)>();
                
                Parallel.ForEach(mensagens, msg =>
                {
                    var json = MensagemSerializer.Serializar(msg);
                    results.Add((msg, json));
                });
                
                return results.All(pair =>
                {
                    var deserialized = MensagemSerializer.Deserializar(pair.Item2);
                    return deserialized.Tipo == pair.Item1.Tipo &&
                           deserialized.SensorId == pair.Item1.SensorId;
                });
            });
    }
}
```

### Custom Generators

Property-based tests require custom generators for Mensagem objects:

```csharp
public static class MensagemGenerators
{
    public static Arbitrary<Mensagem> ValidMensagem()
    {
        var tipoGen = Gen.Elements(TiposMensagem.Validos.ToArray());
        var sensorIdGen = Gen.Elements("SENSOR_001", "SENSOR_002", "SENSOR_999");
        var timestampGen = Gen.Constant(DateTime.UtcNow.ToString("o"));
        
        return Arb.From(
            from tipo in tipoGen
            from sensorId in sensorIdGen
            from timestamp in timestampGen
            from payload in PayloadGen(tipo)
            select new Mensagem(tipo, sensorId, payload, timestamp)
        );
    }
    
    private static Gen<Dictionary<string, object>> PayloadGen(string tipo)
    {
        return tipo switch
        {
            TiposMensagem.REGISTER => Gen.Constant(new Dictionary<string, object>
            {
                ["tipos_dados"] = new List<string> { "temperatura", "humidade" }
            }),
            TiposMensagem.DATA => Gen.Constant(new Dictionary<string, object>
            {
                ["tipo_dado"] = "temperatura",
                ["valor"] = 23.5
            }),
            TiposMensagem.REGISTER_ERR or TiposMensagem.ERROR => Gen.Constant(new Dictionary<string, object>
            {
                ["error_code"] = CodigosErro.SENSOR_NOT_FOUND,
                ["description"] = "Erro de teste"
            }),
            _ => Gen.Constant(new Dictionary<string, object>())
        };
    }
}
```

### Unit Test Coverage

Unit tests should cover:

1. **Message Type Constants** (Requirements 2.1-2.8)
   - Verify all 8 message type constants are defined
   - Verify TiposMensagem.Validos contains all types

2. **Port Constants** (Requirements 8.1-8.2)
   - Verify GATEWAY_PORT = 5000
   - Verify SERVER_PORT = 6000

3. **Factory Methods** (Requirements 11.1-11.10, 12.1-12.4)
   - Test each factory method creates correct message structure
   - Verify DATA messages for each sensor data type
   - Verify error messages contain required fields

4. **Edge Cases**
   - Null payload serialization/deserialization
   - Empty payload serialization/deserialization
   - Messages with large payloads
   - Special characters in string fields

5. **Immutability** (Requirement 6.3)
   - Verify fields cannot be modified after construction
   - Verify init-only properties enforce immutability

### Integration Testing

While not part of the protocol library itself, applications should test:

- End-to-end message flow: Sensor → Gateway → Servidor
- Network transmission and reception
- Message ordering and timing
- Error propagation across components

### Test Organization

```
Tests/
├── Unit/
│   ├── MensagemTests.cs
│   ├── TiposMensagemTests.cs
│   ├── MensagemSerializerTests.cs
│   └── FactoryMethodsTests.cs
├── Properties/
│   ├── SerializationPropertyTests.cs
│   ├── ValidationPropertyTests.cs
│   └── ConcurrencyPropertyTests.cs
└── Generators/
    └── MensagemGenerators.cs
```

### Documentation Testing

Manual verification required for:
- PROTOCOLO.md completeness (Requirements 9.1-9.7)
- Example accuracy in documentation
- Code comments and XML documentation

