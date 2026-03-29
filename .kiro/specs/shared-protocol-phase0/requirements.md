# Requirements Document

## Introduction

Este documento define os requisitos para a Fase 0 do projeto de Sistemas Distribuídos - um protocolo de comunicação partilhado para um sistema de monitorização ambiental urbana. O protocolo permite comunicação entre três componentes (Sensor, Gateway, Servidor) através de TCP/IP, suportando registo de sensores, transmissão de dados ambientais, heartbeats e tratamento de erros.

## Glossary

- **Protocol**: O conjunto de regras e estruturas de mensagens definidas para comunicação entre componentes
- **Message**: Uma unidade de comunicação estruturada trocada entre componentes via TCP/IP
- **Message_Class**: A implementação em C# da estrutura de mensagem
- **Serializer**: O componente responsável por converter objetos Message em formato de transmissão
- **Deserializer**: O componente responsável por converter formato de transmissão em objetos Message
- **Message_Type**: O campo que identifica a categoria da mensagem (REGISTER, DATA, HEARTBEAT, etc.)
- **Sensor_ID**: Identificador único de um sensor no sistema
- **Payload**: Os dados específicos transportados numa mensagem
- **Timestamp**: Marca temporal no formato ISO 8601 (UTC)
- **Gateway_Port**: Porto TCP padrão para comunicação com a Gateway (5000)
- **Server_Port**: Porto TCP padrão para comunicação com o Servidor (6000)
- **Protocol_Documentation**: Ficheiro PROTOCOLO.md que descreve o protocolo completo
- **Round_Trip_Test**: Teste que verifica se serializar e deserializar uma mensagem produz objeto equivalente

## Requirements

### Requirement 1: Message Structure Definition

**User Story:** As a developer, I want a standardized message structure, so that all components can communicate consistently.

#### Acceptance Criteria

1. THE Message_Class SHALL contain a field named "tipo" of type string
2. THE Message_Class SHALL contain a field named "sensor_id" of type string
3. THE Message_Class SHALL contain a field named "payload" of type object or dictionary
4. THE Message_Class SHALL contain a field named "timestamp" of type string in ISO 8601 format
5. THE Message_Class SHALL be implemented as a C# class or struct

### Requirement 2: Message Type Enumeration

**User Story:** As a developer, I want predefined message types, so that I can handle different communication scenarios.

#### Acceptance Criteria

1. THE Protocol SHALL define message type "REGISTER" for sensor registration requests
2. THE Protocol SHALL define message type "REGISTER_OK" for successful registration responses
3. THE Protocol SHALL define message type "REGISTER_ERR" for failed registration responses
4. THE Protocol SHALL define message type "DATA" for sensor data transmission
5. THE Protocol SHALL define message type "DATA_ACK" for data acknowledgment
6. THE Protocol SHALL define message type "HEARTBEAT" for sensor liveness signals
7. THE Protocol SHALL define message type "HEARTBEAT_ACK" for heartbeat acknowledgment
8. THE Protocol SHALL define message type "ERROR" for error notifications

### Requirement 3: Message Serialization

**User Story:** As a developer, I want to serialize messages to strings, so that I can transmit them over TCP/IP.

#### Acceptance Criteria

1. THE Serializer SHALL convert Message_Class objects into string format
2. THE Serializer SHALL use JSON format or a custom delimited format
3. WHEN a Message_Class object is provided, THE Serializer SHALL produce a valid string representation
4. THE Serializer SHALL preserve all field values during serialization
5. THE Serializer SHALL handle null or empty payload fields without errors

### Requirement 4: Message Deserialization

**User Story:** As a developer, I want to deserialize strings to message objects, so that I can process received data.

#### Acceptance Criteria

1. THE Deserializer SHALL convert string format into Message_Class objects
2. THE Deserializer SHALL parse JSON format or custom delimited format
3. WHEN a valid string representation is provided, THE Deserializer SHALL produce a Message_Class object
4. WHEN an invalid string is provided, THE Deserializer SHALL return an error or throw an exception
5. THE Deserializer SHALL populate all Message_Class fields from the string representation

### Requirement 5: Round-Trip Serialization Property

**User Story:** As a developer, I want serialization round-trip guarantees, so that I can trust data integrity.

#### Acceptance Criteria

1. FOR ALL valid Message_Class objects, serializing then deserializing SHALL produce an equivalent object
2. THE Round_Trip_Test SHALL verify that message.tipo remains unchanged after round-trip
3. THE Round_Trip_Test SHALL verify that message.sensor_id remains unchanged after round-trip
4. THE Round_Trip_Test SHALL verify that message.payload remains unchanged after round-trip
5. THE Round_Trip_Test SHALL verify that message.timestamp remains unchanged after round-trip

### Requirement 6: Thread Safety

**User Story:** As a developer, I want thread-safe message operations, so that concurrent usage doesn't cause corruption.

#### Acceptance Criteria

1. WHEN multiple threads serialize messages concurrently, THE Serializer SHALL produce correct results for each thread
2. WHEN multiple threads deserialize messages concurrently, THE Deserializer SHALL produce correct results for each thread
3. THE Message_Class SHALL be immutable or provide thread-safe access to its fields
4. THE Protocol implementation SHALL handle concurrent read operations without blocking

### Requirement 7: Message Validation

**User Story:** As a developer, I want message validation, so that I can detect malformed messages early.

#### Acceptance Criteria

1. WHEN a Message_Class is created, THE Message_Class SHALL validate that tipo is not null or empty
2. WHEN a Message_Class is created, THE Message_Class SHALL validate that tipo matches one of the defined message types
3. WHEN a Message_Class is created with tipo "DATA" or "HEARTBEAT", THE Message_Class SHALL validate that sensor_id is not null or empty
4. WHEN a Message_Class is created, THE Message_Class SHALL validate that timestamp is in valid ISO 8601 format
5. IF validation fails, THEN THE Message_Class SHALL throw an exception with a descriptive error message

### Requirement 8: Default Port Configuration

**User Story:** As a developer, I want predefined default ports, so that components can connect without manual configuration.

#### Acceptance Criteria

1. THE Protocol SHALL define Gateway_Port with value 5000
2. THE Protocol SHALL define Server_Port with value 6000
3. THE Protocol_Documentation SHALL specify Gateway_Port as the default port for sensor-to-gateway communication
4. THE Protocol_Documentation SHALL specify Server_Port as the default port for gateway-to-server communication

### Requirement 9: Protocol Documentation

**User Story:** As a developer, I want comprehensive protocol documentation, so that I can implement components correctly.

#### Acceptance Criteria

1. THE Protocol_Documentation SHALL be created in a file named "PROTOCOLO.md"
2. THE Protocol_Documentation SHALL describe the structure of all message types
3. THE Protocol_Documentation SHALL provide examples of serialized messages for each message type
4. THE Protocol_Documentation SHALL specify the serialization format (JSON or custom)
5. THE Protocol_Documentation SHALL document Gateway_Port and Server_Port values
6. THE Protocol_Documentation SHALL describe the expected message flow for each operation (register, data transmission, heartbeat)
7. THE Protocol_Documentation SHALL document error codes used in REGISTER_ERR and ERROR messages

### Requirement 10: Extensibility for Future Message Types

**User Story:** As a developer, I want an extensible protocol design, so that new message types can be added without breaking existing code.

#### Acceptance Criteria

1. THE Message_Class design SHALL allow new message types to be added without modifying existing message handling code
2. WHEN an unknown message type is received, THE Deserializer SHALL handle it gracefully without crashing
3. THE Protocol SHALL use the payload field to accommodate type-specific data without requiring new fields
4. THE Protocol_Documentation SHALL describe the process for adding new message types

### Requirement 11: Sensor Data Type Support

**User Story:** As a developer, I want the protocol to support multiple sensor data types, so that diverse environmental measurements can be transmitted.

#### Acceptance Criteria

1. THE Protocol SHALL support transmission of temperature data in DATA messages
2. THE Protocol SHALL support transmission of humidity data in DATA messages
3. THE Protocol SHALL support transmission of air quality data in DATA messages
4. THE Protocol SHALL support transmission of noise level data in DATA messages
5. THE Protocol SHALL support transmission of PM2.5 data in DATA messages
6. THE Protocol SHALL support transmission of PM10 data in DATA messages
7. THE Protocol SHALL support transmission of luminosity data in DATA messages
8. THE Protocol SHALL support transmission of image/video data in DATA messages
9. THE payload field in DATA messages SHALL include a "tipo_dado" field to identify the sensor data type
10. THE payload field in DATA messages SHALL include a "valor" field to contain the measurement value

### Requirement 12: Error Message Payload Structure

**User Story:** As a developer, I want structured error messages, so that I can diagnose and handle errors appropriately.

#### Acceptance Criteria

1. WHEN tipo is "REGISTER_ERR", THE payload SHALL contain an "error_code" field
2. WHEN tipo is "REGISTER_ERR", THE payload SHALL contain a "description" field with human-readable error text
3. WHEN tipo is "ERROR", THE payload SHALL contain an "error_code" field
4. WHEN tipo is "ERROR", THE payload SHALL contain a "description" field with human-readable error text
5. THE Protocol_Documentation SHALL define standard error codes including "SENSOR_NOT_FOUND", "SENSOR_INACTIVE", and "SERVER_UNAVAILABLE"
