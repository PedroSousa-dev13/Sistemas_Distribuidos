INDICE COMPLETO DA ENTREGA
==========================

1) DOCUMENTOS PRINCIPAIS
-----------------------------------
- docs/README.md
  Guia geral, comandos, estado atual.

- docs/PROTOCOLO.md
  Especificacao do protocolo de comunicacao (Mensagem, serializacao).

- docs/RABBITMQ_README.md
  Topologia RabbitMQ, exchanges, routing.

- docs/ROUTING_STRATEGY.md
  Estrategia de routing por tipo e zona.

- docs/TESTE_COMPLETO.md
  Guia de testes passo-a-passo.

- docs/MUDANCAS.md
  Historico de alteracoes (Fase 2 - RabbitMQ).

- docs/FASE_3_COMPLETA.md
  Documentacao da Fase 3 (microservicos Python).

- docs/ARQUITETURA.md
  Diagramas e decisoes arquiteturais.


2) CODIGO FONTE POR MODULO
--------------------------
2.1) SharedProtocol
- src/SharedProtocol/Mensagem.cs
- src/SharedProtocol/MensagemSerializer.cs
- src/SharedProtocol/TiposMensagem.cs
- src/SharedProtocol/CodigosErro.cs
- src/SharedProtocol/PortosProtocolo.cs
- src/SharedProtocol/LogHelper.cs

2.2) Sensor
- src/Sensor/Program.cs
- src/Sensor/RabbitMQSensorClient.cs

2.3) DataStreamClient
- src/DataStreamClient/Program.cs

2.4) Gateway
- src/Gateway/Program.cs
- src/Gateway/RabbitMQGatewayClient.cs
- src/Gateway/PreProcessamentoClient.cs
- src/Gateway/SensorInfo.cs

2.5) Servidor
- src/Servidor/Program.cs
- src/Servidor/ServidorMonitor.cs
- src/Servidor/AnaliseClient.cs

2.6) Pre-Processamento (Python)
- src/PreProcessamento/servico.py

2.7) Analise (Python)
- src/Analise/servico.py
- src/Analise/analise_estatistica.py
- src/Analise/detecao_padroes.py

2.8) Interface (Python)
- src/Interface/main.py


3) FICHEIROS DE CONFIGURACAO
---------------------------
- sensores.csv - Fonte de verdade para sensores conhecidos
- docker-compose.yml - Orquestracao Docker (6 servicos)
- Dockerfile.gateway / Dockerfile.servidor / Dockerfile.* - Dockerfiles


4) TESTES
---------
- tests/Gateway.Tests/    - 6 testes (xUnit + Moq)
- tests/Servidor.Tests/   - 8 testes (xUnit + Moq)
- tests/Analise.Tests/    - 25 testes (pytest)


5) SEQUENCIA DE DEMONSTRACAO
----------------------------
Opcao A - Tudo em Docker:
  docker-compose up --build -d

Opcao B - Local:
  Terminal 1: docker-compose up -d rabbitmq pre-processamento analise interface
  Terminal 2: dotnet run --project src/Servidor/Servidor.csproj -- 7000
  Terminal 3: dotnet run --project src/Gateway/Gateway.csproj -- 127.0.0.1:7000 ./sensores.csv
  Terminal 4: dotnet run --project src/Sensor/Sensor.csproj -- sensor-01


6) PERGUNTAS PROVAVEIS
----------------------
"Como funciona a comunicacao?"
- Sensor -> RabbitMQ (AMQP) -> Gateway -> TCP -> Servidor
- Gateway -> Pre-Processamento (HTTP RPC)
- Servidor -> Analise (HTTP RPC)

"Como lidam com falhas?"
- Gateway: watchdog TCP reconecta ao Servidor automaticamente
- RPC: retry com backoff (2 tentativas, delay progressivo)
- RabbitMQ: filas persistentes, conexao auto-recuperavel

"Como garantem consistencia?"
- Mutex nas escritas CSV e contagem de gateways
- SQLite com transacoes no Servidor
- Cleanup de gateways desconectados via try/finally


7) ESTADO FINAL
---------------
- Build: 0 erros
- Testes C#: 14/14 passing
- Testes Python: 25/25 passing
- Docker: 6 servicos orquestrados
- Documentacao: completa para estudo e defesa
