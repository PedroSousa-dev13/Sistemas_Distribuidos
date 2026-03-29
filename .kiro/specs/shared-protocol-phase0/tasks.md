# Implementation Plan: Shared Protocol Phase 0

## Overview

Este plano implementa o protocolo de comunicação partilhado em C# para o sistema de monitorização ambiental urbana. A implementação inclui a classe Mensagem com validação, serialização JSON usando System.Text.Json, factory methods para cada tipo de mensagem, e testes unitários e property-based com FsCheck.

## Tasks

- [x] 1. Criar estrutura do projeto e configuração inicial
  - Criar projeto de biblioteca de classes C# (.NET 6 ou superior)
  - Criar projeto de testes xUnit
  - Adicionar referências: System.Text.Json, FsCheck, FsCheck.Xunit
  - Configurar estrutura de diretórios (src/, tests/)
  - _Requirements: 1.5, 3.2_

- [x] 2. Implementar classes de constantes e enumerações
  - [x] 2.1 Implementar classe TiposMensagem
    - Definir constantes para os 8 tipos de mensagem (REGISTER, REGISTER_OK, REGISTER_ERR, DATA, DATA_ACK, HEARTBEAT, HEARTBEAT_ACK, ERROR)
    - Criar HashSet Validos com todos os tipos
    - Implementar método RequerSensorId(string tipo)
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5, 2.6, 2.7, 2.8_

  - [x] 2.2 Implementar classe CodigosErro
    - Definir constantes para códigos de erro (SENSOR_NOT_FOUND, SENSOR_INACTIVE, SERVER_UNAVAILABLE, INVALID_FORMAT, INVALID_DATA_TYPE)
    - _Requirements: 12.5_

  - [x] 2.3 Implementar classe PortosProtocolo
    - Definir constante GATEWAY_PORT = 5000
    - Definir constante SERVER_PORT = 6000
    - _Requirements: 8.1, 8.2_

  - [x] 2.4 Escrever testes unitários para constantes
    - Verificar que todos os 8 tipos de mensagem estão definidos
    - Verificar que TiposMensagem.Validos contém todos os tipos
    - Verificar valores dos portos
    - Verificar método RequerSensorId para cada tipo
    - _Requirements: 2.1-2.8, 8.1, 8.2_

- [x] 3. Implementar classe Mensagem com validação
  - [x] 3.1 Criar classe Mensagem com propriedades
    - Definir propriedades: Tipo, SensorId, Payload, Timestamp (init-only)
    - Usar Dictionary<string, object> para Payload
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 6.3_

  - [x] 3.2 Implementar construtor com validação completa
    - Validar tipo não nulo/vazio
    - Validar tipo está em TiposMensagem.Validos
    - Validar sensorId para tipos que requerem (DATA, HEARTBEAT, REGISTER)
    - Validar timestamp em formato ISO 8601
    - Lançar ArgumentException com mensagens descritivas
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 3.3 Escrever testes unitários para validação
    - Testar rejeição de tipo nulo/vazio
    - Testar rejeição de tipo inválido
    - Testar rejeição de sensorId vazio para tipos que requerem
    - Testar aceitação de sensorId vazio para tipos que não requerem
    - Testar rejeição de timestamp inválido
    - Testar mensagens de erro descritivas
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

  - [x] 3.4 Escrever property test para validação
    - **Property 5: Null or Empty Tipo Rejection**
    - **Validates: Requirements 7.1**
    - **Property 6: Invalid Tipo Rejection**
    - **Validates: Requirements 7.2**
    - **Property 7: Conditional SensorId Validation**
    - **Validates: Requirements 7.3**
    - **Property 8: Invalid Timestamp Rejection**
    - **Validates: Requirements 7.4**

- [x] 4. Implementar factory methods para criação de mensagens
  - [x] 4.1 Implementar factory methods básicos
    - CriarRegister(sensorId, tiposDados)
    - CriarRegisterOk(sensorId)
    - CriarRegisterErr(sensorId, errorCode, description)
    - CriarHeartbeat(sensorId)
    - CriarHeartbeatAck(sensorId)
    - Usar DateTime.UtcNow.ToString("o") para timestamp
    - _Requirements: 2.1, 2.2, 2.3, 2.6, 2.7, 12.1, 12.2_

  - [x] 4.2 Implementar factory methods para dados e erros
    - CriarData(sensorId, tipoDado, valor)
    - CriarDataAck(sensorId)
    - CriarError(sensorId, errorCode, description)
    - _Requirements: 2.4, 2.5, 2.8, 11.9, 11.10, 12.3, 12.4_

  - [x] 4.3 Escrever testes unitários para factory methods
    - Testar cada factory method cria mensagem com tipo correto
    - Testar estrutura de payload para cada tipo
    - Testar que timestamp é gerado em formato ISO 8601
    - Testar factory methods para todos os tipos de dados de sensor (temperatura, humidade, qualidade_ar, ruido, pm25, pm10, luminosidade, imagem)
    - _Requirements: 2.1-2.8, 11.1-11.10, 12.1-12.4_

- [x] 5. Checkpoint - Validar classe Mensagem
  - Ensure all tests pass, ask the user if questions arise.

- [x] 6. Implementar serialização com System.Text.Json
  - [x] 6.1 Implementar classe MensagemSerializer
    - Criar classe estática MensagemSerializer
    - Configurar JsonSerializerOptions (camelCase, não indentado)
    - Implementar método Serializar(Mensagem) retornando string JSON
    - Validar mensagem não nula
    - Preservar todos os campos durante serialização
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 6.2 Implementar deserialização com tratamento de erros
    - Implementar método Deserializar(string) retornando Mensagem
    - Validar JSON não nulo/vazio
    - Capturar JsonException e lançar FormatException descritiva
    - Validar resultado não nulo
    - Após parse JSON, validar com o construtor parametrizado (tipo, sensor_id, timestamp); `ArgumentException` se semântica inválida
    - Preservar todos os campos durante deserialização
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5_

  - [x] 6.3 Escrever testes unitários para serialização
    - Testar serialização de cada tipo de mensagem
    - Testar deserialização de JSON válido
    - Testar deserialização de JSON inválido lança FormatException
    - Testar serialização com payload nulo/vazio
    - Testar preservação de caracteres especiais
    - _Requirements: 3.1-3.5, 4.1-4.5_

  - [x] 6.4 Escrever property test para round-trip
    - **Property 1: Serialization Round-Trip Preservation**
    - **Validates: Requirements 3.1, 3.4, 4.1, 4.5, 5.1, 5.2, 5.3, 5.4, 5.5**
    - Testar que serializar e deserializar preserva tipo, sensorId, payload e timestamp
    - Mínimo 100 iterações

  - [x] 6.5 Escrever property test para rejeição de JSON inválido
    - **Property 2: Invalid JSON Rejection**
    - **Validates: Requirements 4.4**
    - Testar que JSON inválido lança exceção sem crash
    - Mínimo 100 iterações

- [x] 7. Implementar geradores customizados para property-based testing
  - [x] 7.1 Criar classe MensagemGenerators
    - Implementar gerador ValidMensagem() usando FsCheck
    - Criar geradores para cada campo (tipo, sensorId, timestamp, payload)
    - Payload por tipo via `PayloadForTipo` (conjunto finito tipo × sensor_id)
    - Garantir que mensagens geradas são sempre válidas
    - _Requirements: 5.1-5.5_

  - [x] 7.2 Escrever testes para validar geradores
    - Testar que geradores produzem mensagens válidas
    - Testar distribuição de tipos de mensagem
    - Testar variedade de payloads gerados

- [x] 8. Implementar testes de concorrência
  - [x] 8.1 Escrever property test para serialização concorrente
    - **Property 3: Concurrent Serialization Safety**
    - **Validates: Requirements 6.1**
    - Testar serialização de múltiplas mensagens em paralelo
    - Verificar que cada thread produz JSON correto
    - Mínimo 100 iterações

  - [x] 8.2 Escrever property test para deserialização concorrente
    - **Property 4: Concurrent Deserialization Safety**
    - **Validates: Requirements 6.2**
    - Testar deserialização de múltiplos JSONs em paralelo
    - Verificar que cada thread produz Mensagem correta
    - Mínimo 100 iterações

  - [x] 8.3 Escrever testes unitários para imutabilidade
    - Verificar que propriedades são init-only
    - Verificar que múltiplas threads podem ler mesma mensagem
    - _Requirements: 6.3, 6.4_

- [x] 9. Implementar property test para tratamento de tipos desconhecidos
  - [x] 9.1 Escrever property test para tipos desconhecidos
    - **Property 9: Unknown Message Type Graceful Handling**
    - **Validates: Requirements 10.2**
    - Testar que tipos desconhecidos são tratados graciosamente
    - Verificar que não ocorre crash da aplicação
    - Mínimo 100 iterações

- [x] 10. Validar documentação do protocolo
  - [x] 10.1 Verificar ficheiro PROTOCOLO.md
    - Confirmar que ficheiro existe e está completo
    - Verificar que descreve estrutura de todos os tipos de mensagem
    - Verificar que contém exemplos JSON para cada tipo
    - Verificar que especifica formato de serialização (JSON)
    - Verificar que documenta portos (5000 e 6000)
    - Verificar que descreve fluxo de mensagens para cada operação
    - Verificar que documenta códigos de erro
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6, 9.7_

- [x] 11. Checkpoint final - Executar todos os testes
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marcadas com `*` são opcionais e podem ser ignoradas para MVP mais rápido
- Cada task referencia requirements específicos para rastreabilidade
- Property tests devem ter mínimo 100 iterações conforme especificado no design
- Todos os property tests devem ser anotados com comentário referenciando a propriedade do design
- A implementação usa System.Text.Json (nativo do .NET) para melhor performance
- Classe Mensagem é imutável (init-only properties) para thread-safety automática
- Ficheiro PROTOCOLO.md já foi criado e deve ser validado na task 10.1
