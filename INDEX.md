INDICE COMPLETO DA ENTREGA
==========================

Este indice foi escrito para estudo e navegacao rapida antes da defesa.

1) DOCUMENTOS PRINCIPAIS
------------------------
- README.md
  Guia geral do projeto e comandos de execucao.

- PROTOCOLO.md
  Especificacao formal do protocolo de comunicacao.

- APRESENTACAO.txt
  Guiao completo de defesa oral (versao extensa).

- melhorias.txt
  Resumo tecnico das melhorias e limpeza final.

- comparacao.txt
  Analise comparativa entre a solucao entregue e alternativas.

- aa.txt
  Argumentario de apoio para perguntas de alternativas.

- REDE_DISTRIBUIDA.txt
  Notas para execucao em varios computadores.

- bugs.txt
  Registo de problemas e respetivas correcoes.


2) CODIGO FONTE POR MODULO
--------------------------
2.1) SharedProtocol
- src/SharedProtocol/Mensagem.cs
- src/SharedProtocol/MensagemSerializer.cs
- src/SharedProtocol/TiposMensagem.cs
- src/SharedProtocol/CodigosErro.cs
- src/SharedProtocol/PortosProtocolo.cs

Responsabilidade:
- Definir contrato de mensagens e regras de validacao comuns.

2.2) Sensor
- src/Sensor/Program.cs
- src/Sensor/SensorClient.cs

Responsabilidade:
- Registo do sensor, envio manual de medicoes, heartbeat e rececao de ACK.

2.3) DataStreamClient
- src/DataStreamClient/Program.cs
- src/DataStreamClient/DataStreamReader.cs

Responsabilidade:
- Leitura de CSV e simulacao automatica de envio de dados por multiplos sensores.

2.4) Gateway
- src/Gateway/Program.cs
- src/Gateway/SensorInfo.cs

Responsabilidade:
- Validar sensor no CSV, receber mensagens, gerir estado, encaminhar DATA.

2.5) Servidor
- src/Servidor/Program.cs
- src/Servidor/ServidorMonitor.cs

Responsabilidade:
- Processar DATA e persistir por tipo com sincronizacao segura.


3) FICHEIROS DE DADOS E CONFIGURACAO
------------------------------------
- sensores.csv
  Fonte de verdade para sensores conhecidos e respetivo estado inicial.

- dados/stream_dados.csv
  Conjunto de dados para streaming automatico.

- dados/*.txt
  Saida persistida por tipo de dado.

- .vscode/tasks.json
  Tasks de build e execucao local.

- .vscode/launch.json
  Perfis de debug para componentes.


4) SEQUENCIA OFICIAL DE DEMONSTRACAO
------------------------------------
Passo 1:
- dotnet build SharedProtocol.sln -c Debug

Passo 2:
- Servidor: dotnet run --project src/Servidor/Servidor.csproj -- 6000

Passo 3:
- Gateway: dotnet run --project src/Gateway/Gateway.csproj -- 5000 127.0.0.1:6000 ./sensores.csv

Passo 4:
- Sensor: dotnet run --project src/Sensor/Sensor.csproj -- 127.0.0.1 5000 sensor-01

Passo 5:
- DataStreamClient: dotnet run --project src/DataStreamClient/DataStreamClient.csproj -- 127.0.0.1 5000 dados/stream_dados.csv

Passo 6:
- Pressionar tecla para iniciar stream.

Passo 7:
- Validar ficheiros em dados/ e logs.


5) PERGUNTAS PROVAVEIS E ONDE RESPONDER
---------------------------------------
"Porque TCP e nao UDP?"
- Ver comparacao.txt e aa.txt.

"Como garantem consistencia na escrita?"
- Ver src/Servidor/Program.cs e src/Servidor/ServidorMonitor.cs.

"Como lidam com falhas de sensor?"
- Ver heartbeat/watchdog em src/Gateway/Program.cs.

"Como validam mensagens?"
- Ver src/SharedProtocol/Mensagem.cs.

"Como simulam carga?"
- Ver DataStreamClient em src/DataStreamClient/.


6) ESTADO FINAL DA ENTREGA
--------------------------
- Build: ok.
- Fluxo principal: funcional.
- Documentacao: detalhada para estudo e defesa.
- Alternativas: documentadas como extensao, nao como requisito obrigatorio.


7) ROTEIRO DE ESTUDO (PARA APRESENTAR SEM BLOQUEAR)
---------------------------------------------------
Passo A - dominar fluxo base:
- REGISTER -> DATA -> DATA_ACK -> HEARTBEAT.

Passo B - memorizar funcoes nucleares por modulo:
- SensorClient: IniciarAsync, RegistrarAsync, EnviarMedicaoAsync, HeartbeatLoopAsync.
- Gateway: HandleSensor, ConsumerWorker, SendToServer, WatchdogWorker.
- Servidor: HandleGateway, ProcessarDATA, PersistirMedicao.
- DataStreamClient: CarregarAsync/ObterPorSensor/ProcessarSensorAsync.
- SharedProtocol: construtor Mensagem + serializer + tipos oficiais.

Passo C - preparar 5 respostas curtas:
- concorrencia,
- ACK,
- heartbeat,
- validacao de mensagens,
- justificacao de arquitetura.
