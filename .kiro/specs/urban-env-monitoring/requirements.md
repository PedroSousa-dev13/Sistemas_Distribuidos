# Documento de Requisitos

## Introdução

Este documento descreve os requisitos para o Trabalho Prático 1 de Sistemas Distribuídos: uma infraestrutura de monitorização ambiental urbana simulada. O sistema é composto por três tipos de entidades — SENSOR, GATEWAY e SERVIDOR — que comunicam entre si através de sockets TCP em C#. O objetivo é demonstrar conceitos de sistemas distribuídos como comunicação por sockets, concorrência com threads, sincronização com mutexes e persistência de dados.

---

## Glossário

- **Sistema**: O conjunto completo das entidades SENSOR, GATEWAY e SERVIDOR.
- **Sensor**: Entidade cliente que simula a recolha de dados ambientais e comunica com uma Gateway.
- **Gateway**: Entidade intermédia que valida, agrega e encaminha dados dos Sensores para o Servidor.
- **Servidor**: Entidade central que recebe, armazena e organiza os dados enviados pelas Gateways.
- **Heartbeat**: Mensagem periódica enviada pelo Sensor para indicar que está operacional.
- **Registo**: Processo pelo qual um Sensor se identifica junto da Gateway ao estabelecer ligação.
- **CSV_Sensores**: Ficheiro de configuração da Gateway com o formato `sensor_id:estado:zona:[tipos_dados]:last_sync`.
- **Estado_Sensor**: Valor enumerado que pode ser `ativo`, `manutencao` ou `desativado`.
- **Tipo_Dado**: Categoria de medição ambiental: `temperatura`, `humidade`, `qualidade_ar`, `ruido`, `pm25`, `pm10`, `luminosidade`, `imagem`.
- **Mensagem**: Unidade de comunicação trocada entre entidades, com cabeçalho e payload definidos pelo protocolo.
- **Protocolo**: Conjunto de regras que define a estrutura e sequência das Mensagens trocadas entre entidades.
- **Mutex**: Mecanismo de exclusão mútua usado para garantir acesso sequencial a ficheiros partilhados.
- **Thread**: Unidade de execução concorrente usada para atender múltiplas ligações em simultâneo.

---

## Requisitos

### Requisito 1: Inicialização e Registo do Sensor

**User Story:** Como SENSOR, quero receber o IP da Gateway ao arrancar e estabelecer ligação, para poder registar a minha presença na rede.

#### Critérios de Aceitação

1. THE Sensor SHALL aceitar o endereço IP da Gateway e o porto de destino como parâmetros de linha de comandos no arranque.
2. WHEN o Sensor é iniciado com parâmetros válidos, THE Sensor SHALL estabelecer uma ligação TCP à Gateway no endereço e porto fornecidos.
3. WHEN a ligação TCP é estabelecida, THE Sensor SHALL enviar uma Mensagem de registo contendo o seu identificador único e a lista de Tipo_Dado que recolhe.
4. WHEN a Gateway recebe uma Mensagem de registo, THE Gateway SHALL consultar o CSV_Sensores para verificar se o sensor_id existe.
5. IF o sensor_id não existe no CSV_Sensores, THEN THE Gateway SHALL rejeitar o registo e enviar uma Mensagem de erro ao Sensor com o código `SENSOR_NOT_FOUND`.
6. IF o Estado_Sensor no CSV_Sensores for `manutencao` ou `desativado`, THEN THE Gateway SHALL rejeitar o registo e enviar uma Mensagem de erro ao Sensor com o código `SENSOR_INACTIVE`.
7. WHEN o registo é aceite, THE Gateway SHALL enviar uma Mensagem de confirmação ao Sensor com o código `REGISTER_OK`.
8. IF os parâmetros de arranque do Sensor forem inválidos ou ausentes, THEN THE Sensor SHALL terminar a execução e apresentar uma mensagem de erro descritiva na consola.

---

### Requisito 2: Envio de Medições pelo Sensor

**User Story:** Como SENSOR, quero utilizar uma interface de texto para simular a criação de uma medição ambiental específica.

#### Critérios de Aceitação

1. WHILE o Sensor está registado e com Estado_Sensor `ativo`, THE Sensor SHALL apresentar um menu de texto na consola que permita ao utilizador selecionar o Tipo_Dado a enviar e introduzir o valor da medição.
2. WHEN o utilizador submete uma medição através do menu, THE Sensor SHALL construir uma Mensagem de dados contendo o sensor_id, o Tipo_Dado, o valor da medição e o timestamp de recolha.
3. WHEN a Mensagem de dados é construída, THE Sensor SHALL enviá-la à Gateway através da ligação TCP estabelecida.
4. WHEN a Gateway recebe uma Mensagem de dados de um Sensor com Estado_Sensor `ativo`, THE Gateway SHALL encaminhar a Mensagem para o Servidor.
5. IF a Gateway recebe uma Mensagem de dados de um Sensor com Estado_Sensor diferente de `ativo`, THEN THE Gateway SHALL descartar a Mensagem e enviar uma Mensagem de erro ao Sensor com o código `SENSOR_INACTIVE`.
6. WHEN o Servidor recebe uma Mensagem de dados, THE Servidor SHALL confirmar a receção com uma Mensagem de acknowledgement ao Gateway.

---

### Requisito 3: Mecanismo de Heartbeat

**User Story:** Como SENSOR, quero enviar um heartbeat periódico, para que a Gateway saiba que continuo operacional.

#### Critérios de Aceitação

1. WHILE o Sensor está registado, THE Sensor SHALL enviar uma Mensagem de heartbeat à Gateway em intervalos regulares não superiores a 30 segundos.
2. WHEN a Gateway recebe uma Mensagem de heartbeat de um Sensor registado, THE Gateway SHALL atualizar o campo `last_sync` do sensor correspondente no CSV_Sensores com o timestamp atual.
3. WHILE um Sensor registado não envia qualquer Mensagem durante um período superior a 60 segundos, THE Gateway SHALL marcar o Estado_Sensor desse Sensor como `manutencao` no CSV_Sensores.
4. IF a Gateway não conseguir atualizar o CSV_Sensores após receber um heartbeat, THEN THE Gateway SHALL registar o erro num ficheiro de log local.

---

### Requisito 4: Gestão de Configuração da Gateway (CSV)

**User Story:** Como GATEWAY, quero ler um ficheiro CSV quando um sensor se liga, para validar se ele está registado e qual o seu estado.

#### Critérios de Aceitação

1. THE Gateway SHALL manter um ficheiro CSV_Sensores com o formato `sensor_id:estado:zona:[tipos_dados]:last_sync` para cada Sensor associado.
2. WHEN a Gateway é iniciada, THE Gateway SHALL carregar o conteúdo do CSV_Sensores para memória.
3. WHEN o CSV_Sensores é lido ou escrito por múltiplas Threads em simultâneo, THE Gateway SHALL usar um Mutex para garantir acesso sequencial ao ficheiro.
4. WHEN o Estado_Sensor de um Sensor é alterado, THE Gateway SHALL persistir a alteração no CSV_Sensores imediatamente.
5. IF o ficheiro CSV_Sensores não existir no arranque da Gateway, THEN THE Gateway SHALL criar um ficheiro CSV_Sensores vazio e registar um aviso na consola.
6. THE Gateway SHALL suportar os três valores de Estado_Sensor: `ativo`, `manutencao` e `desativado`.

---

### Requisito 5: Atendimento Concorrente na Gateway

**User Story:** Como GATEWAY, quero processar os dados recebidos de vários sensores ao mesmo tempo usando threads, para não bloquear a comunicação com outros sensores.

#### Critérios de Aceitação

1. WHEN a Gateway recebe uma nova ligação TCP de um Sensor, THE Gateway SHALL criar uma Thread dedicada para gerir toda a comunicação com esse Sensor.
2. WHILE múltiplas Threads estão a processar Sensores em simultâneo, THE Gateway SHALL garantir que o acesso ao CSV_Sensores é protegido por um Mutex.
3. WHEN uma Thread de atendimento de Sensor termina, THE Gateway SHALL libertar os recursos associados a essa Thread.
4. THE Gateway SHALL suportar o atendimento simultâneo de no mínimo 10 Sensores.

---

### Requisito 6: Encaminhamento de Dados para o Servidor

**User Story:** Como GATEWAY, quero encaminhar os dados validados para o Servidor correto, para que a informação seja centralizada.

#### Critérios de Aceitação

1. WHEN a Gateway valida uma Mensagem de dados de um Sensor com Estado_Sensor `ativo`, THE Gateway SHALL estabelecer ou reutilizar uma ligação TCP ao Servidor e encaminhar a Mensagem.
2. THE Gateway SHALL aceitar o endereço IP e porto do Servidor como parâmetros de linha de comandos no arranque.
3. IF a ligação TCP ao Servidor falhar, THEN THE Gateway SHALL registar o erro num ficheiro de log local e notificar o Sensor com o código `SERVER_UNAVAILABLE`.
4. WHEN o Servidor confirma a receção de uma Mensagem, THE Gateway SHALL registar o acknowledgement no log local.

---

### Requisito 7: Armazenamento de Dados no Servidor

**User Story:** Como SERVIDOR, quero receber dados de várias Gateways em simultâneo e usar mutexes para garantir que não corrompo os ficheiros ao gravar os dados por tipo.

#### Critérios de Aceitação

1. WHEN o Servidor recebe uma Mensagem de dados, THE Servidor SHALL persistir o valor da medição num ficheiro dedicado ao Tipo_Dado correspondente (ex: `temperatura.txt`, `humidade.txt`).
2. WHEN múltiplas Threads tentam escrever no mesmo ficheiro de Tipo_Dado em simultâneo, THE Servidor SHALL usar um Mutex por ficheiro para garantir acesso sequencial.
3. THE Servidor SHALL manter um Mutex distinto para cada ficheiro de Tipo_Dado, de forma a maximizar a concorrência entre escritas de tipos diferentes.
4. WHEN o Servidor recebe uma nova ligação TCP de uma Gateway, THE Servidor SHALL criar uma Thread dedicada para gerir a comunicação com essa Gateway.
5. THE Servidor SHALL suportar o atendimento simultâneo de no mínimo 5 Gateways.
6. WHEN os dados são persistidos com sucesso, THE Servidor SHALL enviar uma Mensagem de acknowledgement à Gateway.

---

### Requisito 8: Protocolo de Comunicação

**User Story:** Como Sistema, quero que todas as entidades comuniquem usando um protocolo bem definido, para garantir interoperabilidade e facilidade de depuração.

#### Critérios de Aceitação

1. THE Protocolo SHALL definir um formato de Mensagem com pelo menos os campos: `tipo`, `sensor_id` e `payload`.
2. THE Protocolo SHALL definir os seguintes tipos de Mensagem: `REGISTER`, `REGISTER_OK`, `REGISTER_ERR`, `DATA`, `DATA_ACK`, `HEARTBEAT`, `HEARTBEAT_ACK`, `ERROR`.
3. WHEN uma entidade recebe uma Mensagem com formato inválido, THE entidade SHALL responder com uma Mensagem de tipo `ERROR` e descartar a Mensagem inválida.
4. THE Protocolo SHALL usar codificação UTF-8 para todas as Mensagens de texto.
5. FOR ALL Mensagens de tipo `DATA` enviadas e recebidas, serializar e depois desserializar a Mensagem SHALL produzir um objeto equivalente ao original (propriedade de round-trip).

---

### Requisito 9: Requisitos Não-Funcionais

#### Critérios de Aceitação

1. THE Sistema SHALL ser implementado em C# usando a biblioteca `System.Net.Sockets` para toda a comunicação de rede.
2. THE Sistema SHALL usar `System.Threading.Thread` para criar Threads de atendimento concorrente.
3. THE Sistema SHALL usar `System.Threading.Mutex` ou `System.Threading.Monitor` para proteger o acesso a recursos partilhados.
4. WHEN o Sensor, a Gateway ou o Servidor são iniciados, THE entidade SHALL apresentar na consola o endereço e porto em que está a escutar ou a ligar.
5. THE Sistema SHALL funcionar corretamente num ambiente de rede local (localhost ou LAN).

---

### Requisito 10 (Opcional): Armazenamento em Base de Dados Relacional

**User Story:** Como SERVIDOR, quero opcionalmente armazenar os dados numa base de dados relacional, para facilitar consultas e análise histórica.

#### Critérios de Aceitação

1. WHERE a funcionalidade de base de dados está ativada, THE Servidor SHALL armazenar cada medição recebida numa tabela relacional com os campos `sensor_id`, `tipo_dado`, `valor`, `timestamp` e `zona`.
2. WHERE a funcionalidade de base de dados está ativada, THE Servidor SHALL manter a persistência em ficheiros de texto em paralelo, para garantir compatibilidade com o requisito base.

---

## Arquiteturas Possíveis

### Arquitetura A — Ligações Persistentes (Recomendada)

Cada Sensor mantém uma ligação TCP permanente à Gateway durante toda a sessão. A Gateway mantém uma ligação TCP permanente ao Servidor.

**Vantagens:**
- Menor overhead de estabelecimento de ligação por mensagem
- Heartbeat natural via deteção de socket fechado
- Mais simples de implementar para o contexto académico
- Threads de atendimento têm ciclo de vida claro (1 thread por ligação ativa)

**Desvantagens:**
- Número de ligações simultâneas limitado pelos recursos do SO
- Ligações inativas consomem recursos

**Recomendação:** Este é o caminho mais direto para demonstrar os conceitos do trabalho (sockets, threads, mutexes) com a menor complexidade acidental.

---

### Arquitetura B — Ligações por Pedido (Request-Response)

Cada Sensor abre uma nova ligação TCP para cada mensagem enviada (registo, dados, heartbeat).

**Vantagens:**
- Sem estado de ligação a gerir
- Escalável para muitos sensores esporádicos

**Desvantagens:**
- Overhead elevado de TCP handshake por mensagem
- Heartbeat requer lógica de timeout explícita na Gateway
- Mais complexo para o contexto académico

---

### Arquitetura C — Gateway como Proxy com Fila de Mensagens

A Gateway mantém uma fila interna de mensagens recebidas dos Sensores e uma Thread produtora/consumidora que encaminha para o Servidor.

**Vantagens:**
- Desacopla a velocidade dos Sensores da disponibilidade do Servidor
- Tolerante a falhas temporárias do Servidor

**Desvantagens:**
- Maior complexidade de implementação (produtor-consumidor, fila thread-safe)
- Risco de perda de dados se a Gateway terminar com mensagens na fila
- Excede o âmbito mínimo do trabalho

---

### Caminho Recomendado: Arquitetura A

Para o contexto académico do TP1, a **Arquitetura A (Ligações Persistentes)** é a escolha mais adequada porque:

1. Demonstra claramente o uso de sockets TCP com ligações de longa duração
2. O modelo "1 Thread por ligação" é o padrão mais direto para ensinar concorrência
3. O heartbeat é naturalmente detetável por timeout no socket sem lógica adicional
4. O mutex por ficheiro no Servidor é facilmente demonstrável e testável
5. Minimiza a complexidade acidental, permitindo focar nos conceitos distribuídos

