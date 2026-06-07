# 📘 GUIA DE ESTUDO — Sistemas Distribuídos
## Conceitos para a Apresentação
### Projeto: Sistema de Monitorização Ambiental Urbana

---

## 📋 ÍNDICE RÁPIDO

1. [Microserviços](#1-microserviços)
2. [RabbitMQ & AMQP](#2-rabbitmq--amqp)
3. [Sockets TCP](#3-sockets-tcp)
4. [RPC — Remote Procedure Call](#4-rpc--remote-procedure-call)
5. [Docker & Orquestração](#5-docker--orquestração)
6. [SQLite & Persistência](#6-sqlite--persistência)
7. [Concorrência & Thread-Safety](#7-concorrência--thread-safety)
8. [Resiliência & Tolerância a Falhas](#8-resiliência--tolerância-a-falhas)
9. [Perguntas Frequentes do Professor](#9-perguntas-frequentes-do-professor)

---

## 1. MICROSERVIÇOS

### O que são?
Microserviços é um padrão arquitetural onde uma aplicação é dividida em **serviços pequenos e independentes**. Cada serviço:
- Corre no seu **próprio processo**
- Tem a sua **própria base de dados** (ou pelo menos dados isolados)
- Comunica com outros serviços via **APIs bem definidas**
- Pode ser desenvolvido, testado e deployed **independentemente**

### Monolito vs Microserviços — o que dizer
> "Num monolito, todo o código corre junto — se uma parte falhar, tudo falha. Nos microserviços, cada serviço é independente. Se a Análise Python falhar com uma divisão por zero, o Gateway continua a receber dados, o Servidor continua a persistir em SQLite, e o dashboard continua visível. O utilizador nem repara."

### Os 6 serviços do projeto:
| Serviço | Linguagem | Porta | Função |
|---------|-----------|-------|--------|
| **Sensor** | C# .NET 9 | — | Publica medições no RabbitMQ |
| **Gateway** | C# .NET 9 | — | Consome RabbitMQ, invoca RPC, envia TCP |
| **Servidor** | C# .NET 9 | 7000 | Persiste em SQLite, orquestra análise |
| **Pré-Processamento** | Python 3 | 5001 | Uniformiza/valida dados (RPC) |
| **Análise** | Python 3 | 6001 | Estatísticas, anomalias, previsão (RPC) |
| **Interface** | Python 3 | 8000 | Dashboard web |
| **RabbitMQ** | Erlang | 5672 | Message broker |

### Porquê duas linguagens?
- **C# .NET 9**: Performance de rede, multithreading real (sem GIL), tipagem forte, async/await nativo. Ideal para Sensor, Gateway e Servidor.
- **Python 3**: Bibliotecas científicas (math, numpy-style), prototipagem rápida, Flask para web. Ideal para Análise, Pré-Processamento e Interface.
- **Comunicação entre eles**: HTTP/JSON — o "lingua franca" que ambas entendem.

---

## 2. RABBITMQ & AMQP

### O que é o RabbitMQ?
RabbitMQ é um **message broker** — um intermediário que recebe mensagens de **produtores** e entrega a **consumidores**. Usa o protocolo **AMQP** (Advanced Message Queuing Protocol).

### Analogia simples:
> "É como um sistema de correios: o Sensor deposita uma carta (mensagem) na caixa do correio (exchange), o carteiro (RabbitMQ) encaminha para o destinatário correto (queue do Gateway), e a carta fica guardada até ser entregue."

### Conceitos-chave AMQP:
- **Producer**: Quem envia (Sensor)
- **Exchange**: Recebe e decide para onde encaminhar
- **Queue**: Fila onde as mensagens ficam armazenadas
- **Consumer**: Quem recebe (Gateway)
- **Binding**: Regra que liga uma queue a um exchange
- **Routing Key**: Chave que decide qual queue recebe

### Tipos de Exchange (saber todos!):
| Tipo | Comportamento | Exemplo |
|------|--------------|---------|
| **Direct** | Routing key EXATAMENTE igual | chat.user1 → só user1 recebe |
| **Fanout** | Envia para TODAS as queues | broadcast para todos |
| **Topic** | Padrões com wildcards (`*`, `#`) | ← **USAMOS ESTE!** |
| **Headers** | Baseado nos headers da mensagem | pouco usado |

### Porquê Topic Exchange?
> "Escolhemos Topic porque permite routing flexível com wildcards. O Gateway subscreve `sensor.*.#` e recebe TODAS as medições de qualquer sensor, sem precisar de saber quantos sensores existem. Se amanhã adicionarmos 100 sensores, o Gateway não precisa de mudar nada."

### Wildcards:
- `*` (asterisco) = substitui **exatamente UMA** palavra
- `#` (cardinal) = substitui **ZERO ou mais** palavras

### Exemplos de routing keys no projeto:
```
sensor.sensor-01.temperatura  → medição de temperatura do sensor-01
zona.A.humidade               → medição de humidade da zona A

Gateway subscreve:
  sensor.*.#   → recebe TUDO de qualquer sensor
  zona.*.#     → recebe TUDO de qualquer zona
```

### Dois exchanges no projeto:
1. **sensor-measurements** (Topic, durable) — para medições
2. **sensor-control** (Topic, durable) — para registo, heartbeat, controlo

### Conceitos avançados (perguntas frequentes):
- **Persistent (DeliveryMode=2)**: Mensagens gravadas em disco, sobrevivem a reinicios do RabbitMQ
- **autoAck: false**: O Gateway faz ACK manualmente após processar — se o Gateway cair antes do ACK, o RabbitMQ reenvia
- **Heartbeat AMQP (30s)**: Keep-alive entre cliente e broker — se parar, o broker fecha a ligação
- **AutomaticRecovery**: Se a ligação cair, o cliente reconecta automaticamente

### Porquê RabbitMQ e não Kafka?
> "Kafka é otimizado para streams massivos de dados (milhões de eventos/segundo). Para IoT com 6 sensores, é exagerado. RabbitMQ é mais simples, tem routing mais flexível (topic exchanges com wildcards), e é mais leve para Docker."

---

## 3. SOCKETS TCP

### O que é TCP?
TCP (Transmission Control Protocol) é um protocolo de transporte que fornece uma **ligação bidirecional, fiável e orientada a stream**.

### Características do TCP (saber de cor):
1. **Orientado a stream**: Dados enviados como fluxo contínuo
2. **Fiável**: Garante entrega na ordem correta, sem duplicatas
3. **Bidirecional**: Ambos os lados enviam e recebem
4. **Controlo de fluxo**: Ajusta velocidade ao receptor
5. **Orientado a conexão**: 3-way handshake antes de enviar dados

### Porquê TCP puro e não HTTP para Gateway→Servidor?
> "HTTP adiciona ~200 bytes de headers por cada request. Com payloads de medições de ~150 bytes, os headers representariam mais de 50% do tráfego! Com TCP puro, eliminamos esse overhead. A ligação é reutilizada para centenas de medições — sem handshake por cada uma."

### O protocolo customizado:
- **Formato**: JSON + newline (`\n`)
- **Encoding**: UTF-8
- **Tamanho máximo**: 10 MB por linha
- **Delimitador**: `\n` (newline)
- **Parsing**: `StreamReader.ReadLine()` em C# — trivial!

### Porquê `\n` como delimitador?
> "O newline permite usar `ReadLine()` em C# e `readline()` em Python — parsing automático. Não precisamos de enviar o tamanho da mensagem antecipadamente como nos protocolos binários."

### Tipos de mensagem:
| Tipo | Direção | Função |
|------|---------|--------|
| REGISTER | Sensor → Gateway | Sensor anuncia que existe |
| REGISTER_OK | Gateway → Sensor | Confirma registo |
| DATA | Gateway → Servidor | Envia medição |
| DATA_ACK | Servidor → Gateway | Confirma receção |
| HEARTBEAT | Sensor → Gateway | Keep-alive |
| ERROR | Qualquer | Erro genérico |

### Porquê não UDP?
> "UDP não garante entrega nem ordem. Se um pacote se perder, a medição perde-se sem aviso. Para monitorização ambiental, a fiabilidade é mais importante que a velocidade mínima que UDP oferece."

### Porquê não WebSockets?
> "WebSockets foram pensados para comunicação browser-servidor. Neste caso, Gateway e Servidor são ambos serviços backend. WebSockets adicionam um handshake HTTP upgrade e framing desnecessários."

---

## 4. RPC — REMOTE PROCEDURE CALL

### O que é RPC?
RPC permite **chamar uma função num servidor remoto como se fosse local**. O programador faz uma chamada normal, e o sistema trata de:
1. Serializar argumentos
2. Enviar para o servidor remoto
3. O servidor executa a função
4. Serializa o resultado
5. Devolve ao cliente

### Analogia:
> "É como fazer uma chamada de telefone: ligas (request), a pessoa responde e executa o que pediste (função remota), e diz-te o resultado (response). Não precisas de saber onde está — apenas ligas e ouves."

### HTTP/JSON vs gRPC — o que sabemos:
| Aspeto | HTTP/JSON (usamos) | gRPC (evolução futura) |
|--------|-------------------|----------------------|
| Transport | HTTP/1.1 | HTTP/2 |
| Formato | JSON (texto) | Protobuf (binário) |
| Velocidade | Média | 10x mais rápido |
| Legibilidade | Alta | Baixa |
| Contrato | Fraco (implícito) | Forte (.proto files) |
| Debug | curl, Postman | grpcurl (especializado) |

### Porquê HTTP/JSON e não gRPC?
> "HTTP/JSON é trivial em ambas as linguagens — `HttpClient` em C# e `requests` em Python. Para debug, podemos usar curl ou Postman. Com gRPC, precisaríamos de gerar código a partir de ficheiros .proto, configurar canais, e usar ferramentas especializadas. Num projeto académico com 6 sensores, a simplicidade é mais valiosa que a performance."

### Os 5 endpoints RPC do projeto:
| Endpoint | Serviço | Função |
|----------|---------|--------|
| `/rpc/uniformizar` | PreProc (5001) | Converte unidades (Fahrenheit→Celsius, etc.) |
| `/rpc/validar` | PreProc (5001) | Verifica se valor está dentro dos limites |
| `/rpc/estatisticas` | Análise (6001) | Média, mediana, desvio padrão, Q1, Q3 |
| `/rpc/padroes` | Análise (6001) | Anomalias via Z-Score |
| `/rpc/previsao` | Análise (6001) | Regressão linear + previsão |

### Retry com Backoff Exponencial:
```
Tentativa 1: Falha → aguardar 1 segundo
Tentativa 2: Falha → aguardar 2 segundos
Tentativa 3: Falha → desistir (retornar null)
```

> "O backoff exponencial dá tempo ao serviço para recuperar. Se fizermos retry imediato quando o serviço está sobrecarregado, só pioramos o problema."

---

## 5. DOCKER & ORQUESTRAÇÃO

### O que é Docker?
Docker é uma plataforma de **virtualização por containers**. Cada container é um ambiente isolado com o seu próprio filesystem, rede e processos.

### Container vs VM:
- **VM**: Sistema operativo completo (GB), lento a arrancar
- **Container**: Partilha kernel do host, leve (MB), arranca em segundos

### Docker Compose:
Permite definir e gerir **múltiplos containers** como um sistema único com um ficheiro YAML.

### Porquê Docker e não execução manual?
> "O projeto tem 7 processos. Arrancar cada um manualmente, com as portas corretas, variáveis de ambiente e ordem de dependência, é propenso a erros. Docker Compose resolve com `docker-compose up --build -d` — um comando para arrancar tudo."

### Porquê não Kubernetes?
> "Kubernetes é exagerado para 6 serviços académicos. Exige clusters, pods, services, ingress controllers. Docker Compose resolve o mesmo com 1 ficheiro."

### Conceitos-chave no docker-compose.yml:
- **`sensor-network` (bridge)**: Rede interna isolada — serviços comunicam por nome DNS (ex: `rabbitmq`, `servidor`)
- **`depends_on` + `condition: service_healthy`**: Gateway só arranca quando RabbitMQ está realmente saudável
- **Volumes**: `./dados:/app/dados` — dados persistem fora do container
- **Healthchecks**: Verificação periódica de saúde de cada serviço
- **`restart: unless-stopped`**: Reinício automático se o serviço cair

### Variáveis de ambiente (porquê?):
> "Containers Docker são projetados para receber configuração via variáveis de ambiente — é o método recomendado. Podemos alterar portas, hosts e URLs sem recompilar código."

---

## 6. SQLITE & PERSISTÊNCIA

### O que é SQLite?
SQLite é uma base de dados **embutida** (embedded) — corre dentro da mesma aplicação, sem servidor dedicado. Os dados ficam num único ficheiro `.db`.

### Porquê SQLite e não PostgreSQL/MongoDB?
> "PostgreSQL/MongoDB exigem container separado e configuração complexa. SQLite é zero configuração — o ficheiro .db é auto-contido. Com 6 sensores a 1 medição/5s (~1000 medições/hora), SQLite aguenta facilmente — suporta 100K+ writes/segundo."

### Tabelas do projeto:
```sql
-- Tabela de medições
CREATE TABLE medicoes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sensor_id TEXT NOT NULL,
    tipo_dado TEXT NOT NULL,
    valor REAL NOT NULL,
    timestamp TEXT NOT NULL,
    payload_json TEXT
);

-- Tabela de análises
CREATE TABLE analises (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    sensor_id TEXT NOT NULL,
    tipo_dado TEXT NOT NULL,
    tipo_analise TEXT NOT NULL,  -- 'estatisticas', 'padroes', 'previsao'
    resultado TEXT NOT NULL,      -- JSON com resultado completo
    timestamp TEXT NOT NULL
);
```

### ACID:
SQLite suporta **ACID** (Atomicidade, Consistência, Isolamento, Durabilidade) — mesmo que o container caia a meio de uma escrita, a BD não corrompe.

---

## 7. CONCORRÊNCIA & THREAD-SAFETY

### Porquê é importante?
O Servidor aceita **múltiplas Gateways simultaneamente** — cada Gateway corre numa thread separada. Se duas threads tentarem escrever no SQLite ao mesmo tempo sem proteção, os dados corrompem.

### Mecanismos usados:
| Mecanismo | Onde | Porquê |
|-----------|------|--------|
| **Mutex** | `gatewayCount`, `gatewayMap` | Proteger contador e mapa de Gateways |
| **lock** | `_dbLock` no ServidorMonitor | Proteger escrita/leitura na BD |
| **lock** | `gatewayMessagesLock` | Proteger lista de mensagens |
| **ConcurrentQueue** | Gateway RabbitMQ | Fila thread-safe para mensagens |

### O que é um Mutex?
> "Um Mutex (Mutual Exclusion) é um mecanismo de sincronização que garante que apenas UMA thread de cada vez pode aceder a um recurso partilhado. É como uma casa de banho com fechadura — só entra um de cada vez."

### O que é o GIL do Python?
> "O GIL (Global Interpreter Lock) impede que duas threads Python executem código ao mesmo tempo. Por isso usamos C# para o Servidor — C# tem multithreading real."

---

## 8. RESILIÊNCIA & TOLERÂNCIA A FALHAS

### TCP Watchdog (Gateway):
- Verifica a cada **30 segundos** se a ligação ao Servidor está ativa
- Se detectar desconexão → **ReconectarAsync()**
- **20 tentativas máximo**, **5 segundos entre tentativas**
- Também verifica sensores inativos (>60s sem dados)

### Retry com Backoff (RPC):
- Chamadas RPC falham → retry até **2 vezes**
- Delay progressivo: 1s, 2s (backoff exponencial)
- Após 3 falhas → desistir e retornar null

### AutomaticRecovery (RabbitMQ):
- Se a ligação ao broker cair, reconecta automaticamente
- `AutomaticRecoveryEnabled = true` no ConnectionFactory
- Heartbeat AMQP de 30s para detetar desconexão

### Healthchecks (Docker):
- **Python**: `GET /health` retorna 200 OK
- **C#**: `grep -q Gateway /proc/1/cmdline`
- **RabbitMQ**: `rabbitmq-diagnostics -q ping`
- O Docker só marca como "healthy" se o healthcheck passar

### Graceful Shutdown (Python):
- Serviços Python tratam SIGTERM e SIGINT
- Fecham ligações, libertam recursos, exit code 0

### Logs com Rotação:
- LogHelper.cs com rotação de 5 MB
- 5MB ≈ 50.000 linhas de log — suficiente para debug
- Previne que o disco fique cheio

---

## 9. PERGUNTAS FREQUENTES DO PROFESSOR

### "Porquê microserviços e não monolito?"
> "O projeto exige C# e Python — num monolito seria impossível usar ambas. Além disso, o isolamento de falhas é crítico: se a Análise falhar, o sistema continua a funcionar. E podemos escalar granularmente — duplicar só o Servidor se for preciso."

### "Porquê RabbitMQ e não Kafka?"
> "Kafka é para streams massivos — milhões de eventos/segundo. Para IoT com 6 sensores, RabbitMQ é mais simples, mais leve, e tem routing mais flexível com topic exchanges e wildcards."

### "Porquê TCP puro e não HTTP?"
> "HTTP adiciona ~200 bytes de headers por pedido. Com payloads de 150 bytes, mais de metade seria overhead. TCP puro elimina isso — ligação persistente reutilizada para todas as medições."

### "Porquê HTTP/JSON para RPC e não gRPC?"
> "Simplicidade. HTTP/JSON é trivial em C# e Python, debugável com curl, sem geração de código. Para 6 sensores, a performance de gRPC não justifica a complexidade. Os ficheiros .proto existem como referência para evolução futura."

### "Como garantem thread-safety?"
> "Mutex para o contador de Gateways, lock no ServidorMonitor para escrita em SQLite, ConcurrentQueue no Gateway para mensagens do RabbitMQ. Cada Gateway tem a sua thread — o Servidor aceita múltiplas simultâneas."

### "O que acontece se um serviço falhar?"
> "Depende do serviço:
> - Se a Análise cair → o Servidor continua a persistir, a interface mostra os dados existentes
> - Se o RabbitMQ cair → AutomaticRecovery reconecta; mensagens persistentes sobrevivem ao reinício
> - Se o Servidor cair → o Watchdog do Gateway detecta e tenta reconectar (20 tentativas, 5s entre cada)
> - Se o Gateway cair → o RabbitMQ guarda as mensagens na queue até o Gateway voltar"

### "Porquê SQLite e não PostgreSQL?"
> "SQLite é embutido, zero configuração, e suporta 100K+ writes/segundo. Com 6 sensores (~1000 medições/hora), não precisamos de um SGBD dedicado. Menos um container para gerir, menos complexidade."

### "Como funciona o Z-Score para anomalias?"
> "O Z-Score mede quantos desvios padrão um valor está da média. Se |z| > 2.0, consideramos anomalia moderada. Se |z| > 3.0, é crítica. Exemplo: se a temperatura média é 25°C com desvio de 3°C, um valor de 35°C tem z=(35-25)/3=3.33 → anomalia crítica."

### "Porquê backoff exponencial?"
> "Se o serviço RPC estiver sobrecarregado, fazer retry imediato só piora. O backoff dá tempo para recuperar: 1s na 1ª tentativa, 2s na 2ª. É um padrão standard em sistemas distribuídos para evitar 'thundering herd'."

### "Porquê Docker Compose e não Kubernetes?"
> "Kubernetes exige clusters, pods, services, ingress controllers — curva de aprendizagem enorme para 6 serviços. Docker Compose resolve com um ficheiro YAML e `docker-compose up`."

### "O que é a rede bridge sensor-network?"
> "É uma rede interna do Docker onde todos os containers comunicam por nomes DNS (ex: `rabbitmq`, `servidor`, `pre-processamento`) sem expor portas desnecessárias ao exterior. É isolamento de rede."

### "Porquê JSON e não binário (Protobuf)?"
> "JSON é legível por humanos — essencial para debug. Com `netcat` ou `curl` podemos ver exatamente o que foi enviado. Protobuf seria mais compacto mas perde legibilidade. Para medições de sensores com poucos campos, a diferença de tamanho é irrelevante."

---

## 🎯 CHECKLIST ANTES DA APRESENTAÇÃO

- [ ] Saber explicar os 3 paradigmas de comunicação (Pub/Sub, TCP, RPC)
- [ ] Saber explicar porquê cada decisão tecnológica foi tomada
- [ ] Saber o que são exchanges, queues, bindings, routing keys
- [ ] Saber a diferença entre Direct, Fanout, Topic exchange
- [ ] Saber o que é autoAck, Persistent, Heartbeat AMQP
- [ ] Saber explicar o protocolo TCP customizado (JSON + \n)
- [ ] Saber o que é RPC e a diferença entre HTTP/JSON e gRPC
- [ ] Saber explicar retry com backoff exponencial
- [ ] Saber explicar Mutex, lock, ConcurrentQueue
- [ ] Saber o que é SQLite ACID e porquê foi escolhido
- [ ] Saber explicar healthchecks e graceful shutdown
- [ ] Saber explicar o Watchdog TCP e reconexão automática
- [ ] Saber o que acontece quando cada serviço falha
- [ ] Ter o sistema a correr: `docker-compose up --build -d`

---

**Boa sorte na apresentação! 🚀**
