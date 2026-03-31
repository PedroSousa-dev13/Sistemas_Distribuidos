# Distribuição de Trabalho — TP1 Sistemas Distribuídos
# Arquitetura Híbrida A+ | 3 Membros

---

## FASE 0 — Protocolo Partilhado (TODOS)
> Deve ser concluído antes de qualquer desenvolvimento individual.

### Definição do Protocolo de Mensagens
- [x] Definir estrutura base da mensagem (campos: `tipo`, `sensor_id`, `payload`, `timestamp`)
- [x] Definir todos os tipos de mensagem: `REGISTER`, `REGISTER_OK`, `REGISTER_ERR`, `DATA`, `DATA_ACK`, `HEARTBEAT`, `HEARTBEAT_ACK`, `ERROR`
- [x] Implementar classe/struct `Mensagem` em C#
- [x] Implementar serialização para string (ex: JSON ou formato custom delimitado)
- [x] Implementar deserialização de string para objeto `Mensagem`
- [x] Testar round-trip: serializar → deserializar → objeto igual ao original
- [x] Definir porto padrão da Gateway (ex: 5000) e do Servidor (ex: 6000)
- [x] Documentar o protocolo num ficheiro `PROTOCOLO.md`

---

## ALUNO 1 — SENSOR


### 1.1 Arranque e Configuração
- [x] Aceitar endereço IP da Gateway como argumento de linha de comandos (`args[0]`)
- [x] Aceitar porto da Gateway como argumento de linha de comandos (`args[1]`)
- [x] Aceitar ID do sensor como argumento de linha de comandos (`args[2]`)
- [x] Validar que os argumentos estão presentes e são válidos
- [x] Apresentar mensagem de erro descritiva se argumentos inválidos e terminar
- [x] Apresentar na consola o IP e porto da Gateway a que se vai ligar

### 1.2 Ligação TCP à Gateway
- [x] Criar `TcpClient` e estabelecer ligação ao IP:Porto da Gateway
- [x] Tratar exceção se a Gateway não estiver disponível (mensagem de erro + terminar)
- [x] Obter `NetworkStream` para leitura e escrita
- [x] Manter a ligação TCP aberta durante toda a sessão

### 1.3 Registo na Gateway
- [x] Construir mensagem `REGISTER` com `sensor_id` e lista de tipos de dados suportados
- [x] Enviar mensagem `REGISTER` pela ligação TCP
- [x] Aguardar resposta da Gateway
- [x] Se resposta for `REGISTER_OK`: avançar para o menu principal
- [x] Se resposta for `REGISTER_ERR` com código `SENSOR_NOT_FOUND`: apresentar erro e terminar
- [x] Se resposta for `REGISTER_ERR` com código `SENSOR_INACTIVE`: apresentar erro e terminar
- [x] Tratar timeout de resposta (sem resposta em X segundos → terminar)

### 1.4 Interface de Texto (Menu Principal)
- [x] Apresentar menu com opções de tipos de dados disponíveis para o sensor
- [x] Opção: enviar medição de temperatura
- [x] Opção: enviar medição de humidade
- [x] Opção: enviar medição de qualidade do ar
- [x] Opção: enviar medição de ruído
- [x] Opção: enviar medição de PM2.5
- [x] Opção: enviar medição de PM10
- [x] Opção: enviar medição de luminosidade
- [x] Opção: enviar imagem/vídeo (pode ser simulado com string)
- [x] Opção: sair (fechar ligação e terminar)
- [x] Validar input do utilizador (tipo e valor da medição)
- [x] Construir mensagem `DATA` com `sensor_id`, `tipo_dado`, `valor` e `timestamp`
- [x] Enviar mensagem `DATA` pela ligação TCP
- [x] Aguardar `DATA_ACK` da Gateway
- [x] Apresentar confirmação de envio ao utilizador após receber ACK

### 1.5 Thread de Heartbeat
- [x] Criar thread dedicada para envio de heartbeats (separada da thread do menu)
- [x] Enviar mensagem `HEARTBEAT` com `sensor_id` e `timestamp` a cada 20 segundos
- [x] Aguardar `HEARTBEAT_ACK` da Gateway
- [x] Se não receber ACK em X segundos: registar aviso na consola
- [x] Thread de heartbeat deve terminar quando o sensor fechar a ligação
- [x] Garantir que heartbeat e envio de dados não colidem (sincronização se necessário)

### 1.6 Tratamento de Erros e Desligação
- [x] Tratar receção de mensagem `ERROR` da Gateway
- [x] Tratar perda de ligação TCP (exceção de socket)
- [x] Fechar `NetworkStream` e `TcpClient` ao terminar
- [x] Garantir que a thread de heartbeat termina antes de fechar o programa

---

## ALUNO 2 — GATEWAY

### 2.1 Arranque e Configuração
- [ ] Aceitar porto de escuta para sensores como argumento (`args[0]`)
- [ ] Aceitar IP:Porto do Servidor como argumento (`args[1]`)
- [ ] Aceitar caminho do ficheiro CSV como argumento (`args[2]`)
- [ ] Apresentar na consola o porto em que está a escutar
- [ ] Carregar CSV de sensores para memória no arranque

### 2.2 Gestão do Ficheiro CSV
- [ ] Ler ficheiro CSV com formato `sensor_id:estado:zona:[tipos_dados]:last_sync`
- [ ] Parsear cada linha para estrutura de dados em memória (ex: `Dictionary<string, SensorInfo>`)
- [ ] Se ficheiro CSV não existir: criar ficheiro vazio e registar aviso
- [ ] Implementar `Mutex` para proteger leitura/escrita do CSV
- [ ] Implementar método `LerCSV()` com lock do mutex
- [ ] Implementar método `EscreverCSV()` com lock do mutex
- [ ] Implementar método `AtualizarEstado(sensor_id, novo_estado)` com lock
- [ ] Implementar método `AtualizarLastSync(sensor_id, timestamp)` com lock
- [ ] Persistir alterações ao CSV imediatamente após cada modificação

### 2.3 Atendimento de Sensores (Concorrência)
- [ ] Criar `TcpListener` no porto de escuta configurado
- [ ] Loop principal: aceitar novas ligações TCP de sensores
- [ ] Para cada nova ligação: criar nova `Thread` dedicada
- [ ] Thread de atendimento: gerir todo o ciclo de vida da ligação com aquele sensor
- [ ] Suportar mínimo de 10 sensores em simultâneo
- [ ] Libertar recursos da thread quando sensor desliga

### 2.4 Processamento de Mensagens dos Sensores
- [ ] Receber e parsear mensagem `REGISTER`
- [ ] Validar `sensor_id` no CSV (existe?)
- [ ] Validar estado do sensor no CSV (`ativo`?)
- [ ] Se inválido: enviar `REGISTER_ERR` com código adequado
- [ ] Se válido: enviar `REGISTER_OK`
- [ ] Receber mensagem `DATA` de sensor registado e ativo
- [ ] Se sensor não ativo: enviar `ERROR` com código `SENSOR_INACTIVE` e descartar
- [ ] Se sensor ativo: colocar mensagem na fila interna (produtor)
- [ ] Receber mensagem `HEARTBEAT`
- [ ] Atualizar `last_sync` no CSV ao receber heartbeat
- [ ] Enviar `HEARTBEAT_ACK` ao sensor
- [ ] Tratar mensagens com formato inválido: responder com `ERROR` e descartar

### 2.5 Fila Interna Thread-Safe (Produtor-Consumidor)
- [ ] Implementar `BlockingCollection<Mensagem>` como fila interna
- [ ] Threads de receção (produtores) colocam mensagens `DATA` na fila
- [ ] Criar thread consumidora única dedicada ao encaminhamento
- [ ] Thread consumidora retira mensagens da fila e encaminha para o Servidor
- [ ] Fila deve funcionar corretamente com múltiplos produtores simultâneos
- [ ] Definir capacidade máxima da fila (ex: 100 mensagens)

### 2.6 Monitorização de Heartbeats (Timeout)
- [ ] Criar thread de watchdog que verifica `last_sync` de todos os sensores
- [ ] Se sensor não envia mensagem há mais de 60 segundos: marcar como `manutencao`
- [ ] Persistir alteração de estado no CSV
- [ ] Registar evento no ficheiro de log

### 2.7 Ligação ao Servidor e Encaminhamento
- [ ] Estabelecer ligação TCP persistente ao Servidor no arranque
- [ ] Thread consumidora envia mensagens `DATA` da fila para o Servidor
- [ ] Aguardar `DATA_ACK` do Servidor após cada envio
- [ ] Registar ACK no ficheiro de log local
- [ ] Se ligação ao Servidor falhar: registar erro no log e enviar `ERROR` ao sensor com `SERVER_UNAVAILABLE`
- [ ] Tentar reconectar ao Servidor após falha (retry com intervalo)

### 2.8 Logging
- [ ] Criar ficheiro de log `gateway.log`
- [ ] Registar cada registo de sensor (aceite ou rejeitado)
- [ ] Registar cada mensagem DATA recebida e encaminhada
- [ ] Registar cada heartbeat recebido
- [ ] Registar erros de ligação ao Servidor
- [ ] Registar sensores marcados como `manutencao` por timeout
- [ ] Proteger escrita no log com mutex

---

## ALUNO 3 — SERVIDOR

### 3.1 Arranque e Configuração
- [ ] Aceitar porto de escuta para gateways como argumento (`args[0]`)
- [ ] Apresentar na consola o porto em que está a escutar
- [ ] Criar diretório de dados se não existir

### 3.2 Atendimento de Gateways (Concorrência)
- [ ] Criar `TcpListener` no porto de escuta configurado
- [ ] Loop principal: aceitar novas ligações TCP de gateways
- [ ] Para cada nova ligação: criar nova `Thread` dedicada
- [ ] Suportar mínimo de 5 gateways em simultâneo
- [ ] Libertar recursos da thread quando gateway desliga

### 3.3 Receção e Parsing de Mensagens
- [ ] Receber mensagens `DATA` das gateways
- [ ] Parsear mensagem: extrair `sensor_id`, `tipo_dado`, `valor`, `timestamp`
- [ ] Tratar mensagens com formato inválido: responder com `ERROR` e descartar
- [ ] Enviar `DATA_ACK` após persistência bem-sucedida

### 3.4 Persistência em Ficheiros por Tipo de Dado
- [ ] Criar ficheiro `temperatura.txt` para medições de temperatura
- [ ] Criar ficheiro `humidade.txt` para medições de humidade
- [ ] Criar ficheiro `qualidade_ar.txt` para medições de qualidade do ar
- [ ] Criar ficheiro `ruido.txt` para medições de ruído
- [ ] Criar ficheiro `pm25.txt` para medições de PM2.5
- [ ] Criar ficheiro `pm10.txt` para medições de PM10
- [ ] Criar ficheiro `luminosidade.txt` para medições de luminosidade
- [ ] Criar ficheiro `imagem.txt` para dados de imagem/vídeo
- [ ] Formato de cada linha: `timestamp | sensor_id | valor`
- [ ] Implementar um `Mutex` distinto por ficheiro de tipo de dado
- [ ] Método `EscreverMedicao(tipo, linha)` com lock do mutex correspondente
- [ ] Garantir que escritas de tipos diferentes ocorrem em paralelo (sem bloqueio cruzado)
- [ ] Garantir que escritas do mesmo tipo são sequenciais (sem corrupção)

### 3.5 Tratamento de Erros e Desligação
- [ ] Tratar perda de ligação TCP de uma gateway
- [ ] Fechar `NetworkStream` e socket ao terminar ligação
- [ ] Garantir que mutexes são libertados mesmo em caso de exceção (try/finally)

### 3.6 (Opcional) Base de Dados Relacional
- [ ] Configurar ligação a base de dados SQLite ou SQL Server LocalDB
- [ ] Criar tabela `medicoes` com campos: `id`, `sensor_id`, `tipo_dado`, `valor`, `timestamp`, `zona`
- [ ] Inserir cada medição recebida na tabela
- [ ] Manter persistência em ficheiros de texto em paralelo
- [ ] Tratar erros de ligação à base de dados sem interromper o fluxo principal

### 3.7 Logging
- [ ] Criar ficheiro de log `servidor.log`
- [ ] Registar cada ligação de gateway aceite
- [ ] Registar cada medição recebida e persistida
- [ ] Registar erros de escrita em ficheiros
- [ ] Proteger escrita no log com mutex

---

## Pontos de Integração (Todos)

- [x] Fase 0 concluída antes de desenvolvimento individual
- [ ] Teste de ligação Sensor → Gateway com mensagem REGISTER
- [ ] Teste de envio de DATA: Sensor → Gateway → Servidor
- [ ] Teste de heartbeat: Sensor → Gateway (atualização CSV)
- [ ] Teste de timeout: sensor silencioso → Gateway marca como `manutencao`
- [ ] Teste de concorrência: 3+ sensores ligados em simultâneo
- [ ] Teste de fila: Gateway com Servidor desligado (mensagens ficam na fila)
- [ ] Teste de mutex: escrita simultânea no mesmo ficheiro de tipo de dado
- [ ] Teste end-to-end completo com todos os componentes ativos
