# GUIA TECNICO DETALHADO
## Sistema de Monitorizacao Ambiental Urbana
### Projeto de Sistemas Distribuidos - 2025/2026

---

## INDICE

1. [Visao Geral do Sistema](#1-visao-geral-do-sistema)
2. [Arquitetura de Microservicos](#2-arquitetura-de-microservicos)
3. [RabbitMQ e o Protocolo AMQP](#3-rabbitmq-e-o-protocolo-amqp)
4. [Sockets TCP — Comunicacao Direta](#4-sockets-tcp--comunicacao-direta)
5. [Chamadas RPC — Remote Procedure Call](#5-chamadas-rpc--remote-procedure-call)
6. [gRPC e Protocol Buffers](#6-grpc-e-protocol-buffers)
7. [Stack Tecnologica Completa](#7-stack-tecnologica-completa)
8. [Fluxo de Dados Completo](#8-fluxo-de-dados-completo)
9. [Como Executar](#9-como-executar)
10. [Conceitos Avancados](#10-conceitos-avancados)

---

## 1. VISAO GERAL DO SISTEMA

```
=== SISTEMA DE MONITORIZACAO AMBIENTAL URBANA ===
        Projeto de Sistemas Distribuidos - 2025/2026

============================================================
ARQUITETURA
============================================================

Sensor --> RabbitMQ --> Gateway --TCP--> Servidor --> SQLite
                            |                |
                     Pre-Processamento   Analise (RPC)
                       (RPC 5001)         (RPC 6001)
                                              |
                                        Interface Web
                                          (porta 8000)

6 microservicos orquestrados em Docker:
  - rabbitmq: message broker (porta 5672)
  - pre-processamento: uniformizacao de dados (porta 5001)
  - analise: estatisticas, anomalias, previsao (porta 6001)
  - interface: dashboard web (porta 8000)
  - gateway: consume RabbitMQ, RPC, encaminha ao servidor
  - servidor: persistencia SQLite, invoca analise (porta 7000)

============================================================
STACK TECNOLOGICA
============================================================

  - C# .NET 9.0: Sensor, DataStreamClient, Gateway, Servidor
  - Python 3: Pre-Processamento, Analise, Interface
  - RabbitMQ 3.13: mensagens (pub/sub topic)
  - SQLite: persistencia de medicoes e analises
  - Docker: 6 servicos em rede interna

============================================================
FLUXO DE DADOS
============================================================

  1. Sensor publica medicao no RabbitMQ (exchange topic)
     Routing key: sensor.<sensor_id>.<tipo_dado>

  2. Gateway consome a mensagem, invoca Pre-Processamento
     via RPC HTTP para validar/uniformizar

  3. Gateway envia dados tratados ao Servidor via TCP

  4. Servidor persiste em SQLite e invoca Analise via RPC
     quando necessario (estatisticas, anomalias, previsao)

  5. Interface web consulta SQLite (via servidor) e
     mostra dashboard em tempo real

============================================================
COMO EXECUTAR
============================================================

1) TUDO EM DOCKER (recomendado):
   -------------------------------
   docker-compose up --build -d

   A interface web fica em http://localhost:8000

2) LANCAR SENSORES SIMULADOS (Python):
   ------------------------------------
   python scripts/simular_sensores.py

   6 sensores virtuais com dados realistas:
    - sensor-temperatura-01 (zona A): temperatura, humidade
    - sensor-qualidade-01   (zona B): qualidade_ar, pm25, pm10
    - sensor-ruido-01       (zona A): ruido
    - sensor-luz-01         (zona C): luminosidade
    - sensor-ambiente-01    (zona B): temperatura, humidade, q_ar
    - sensor-particulas-01  (zona C): pm25, pm10, ruido

   Intervalo personalizado (segundos entre medicoes):
   python scripts/simular_sensores.py 2

3) LANCAR SENSOR INDIVIDUAL (C#, local):
   --------------------------------------
   cd src\Sensor
   dotnet run sensor-01 localhost 5672

   Escolher tipo de medicao no menu interativo.

============================================================
VISUALIZAR DADOS
============================================================

  - Dashboard: http://localhost:8000
  - RabbitMQ UI: http://localhost:15672 (guest/guest)
  - Logs Docker: docker-compose logs -f gateway

============================================================
TESTES
============================================================

  Testes C# (14 testes):
    dotnet test tests\Gateway.Tests\Gateway.Tests.csproj
    dotnet test tests\Servidor.Tests\Servidor.Tests.csproj

  Testes Python (25 testes):
    cd tests\Analise.Tests
    python -m pytest -v

============================================================
NOTAS
============================================================

  - Projeto compilado em .NET 9.0 (nao 8.0)
  - Todos os servicos Docker tem healthchecks
  - Logs com rotacao automatica (5 MB)
  - URLs RPC configuradas via variaveis de ambiente
  - Sem autenticacao (projeto academico)
```

---

## 2. ARQUITETURA DE MICROSERVICOS

### 2.1 O que sao Microservicos?

Microservicos e um padrao de arquitetura de software onde uma aplicacao e dividida em **servicos pequenos e independentes**, cada um responsavel por uma funcionalidade especifica. Cada servico corre no seu proprio processo, tem a sua propria base de dados e comunica com outros servicos atraves de APIs bem definidas.

**Diferenca entre Monolito e Microservicos:**

```
MONOLITO:                          MICROSERVICOS:
┌──────────────────────┐           ┌─────────┐  ┌─────────┐  ┌─────────┐
│  App Unica           │           │ Sensor  │  │ Gateway │  │ Servidor│
│  ┌────────────────┐  │           └────┬────┘  └────┬────┘  └────┬────┘
│  │ UI + Logica    │  │                │            │            │
│  │ + BaseDados    │  │           ┌────┴────┐  ┌───┴───┐  ┌─────┴────┐
│  │ Tudo junto     │  │           │RabbitMQ │  │PreProc│  │ SQLite   │
│  └────────────────┘  │           └─────────┘  └───────┘  └──────────┘
│  Um so container     │           Containers independentes
└──────────────────────┘
```

**Vantagens dos microservicos neste projeto:**

| Vantagem | Explicacao | Exemplo no Projeto |
|----------|-----------|-------------------|
| **Separacao de responsabilidades** | Cada servico faz uma coisa bem feita | Sensor so publica dados, Analise so calcula estatisticas |
| **Escalabilidade horizontal** | Podemos duplicar um servico sem afetar outros | Se houver muitos dados, duplicamos o Servidor |
| **Tecnologias mistas** | Cada servico usa a melhor linguagem para a sua tarefa | C# para performance, Python para ciencia de dados |
| **Isolamento de falhas** | Se um servico cair, os outros continuam a funcionar | Se a Analise cair, o Servidor continua a persistir dados |
| **Deploy independente** | Podemos atualizar um servico sem redesenhar tudo | Atualizar o Python da Analise sem mexer no C# do Gateway |
| **Testabilidade** | Cada servico e testado de forma isolada | 14 testes C# + 25 testes Python = 39 testes no total |

**Porque microservicos e nao monolito neste projeto:**

O padrao monolito seria mais simples de desenvolver no inicio, mas traria problemas significativos num sistema de monitorizacao ambiental distribuido:

1. **Tecnologias mistas obrigatórias**: O projeto exige C# para componentes de alta performance (Sensor, Gateway, Servidor) e Python para analise cientifica (numpy, scipy). Numa arquitetura monolito, teriamos de escolher uma unica linguagem — ou perderiamos a performance do C# ou as bibliotecas cientificas do Python. Com microservicos, cada servico usa a linguagem mais adequada.

2. **Isolamento de falhas critico**: Num sistema de monitorizacao, se o modulo de analise estatistica falhar (ex: divisao por zero num calculo), nao pode derrubar o sistema inteiro. Com microservicos, a Analise pode cair e o Gateway continua a receber dados, o Servidor continua a persistir, e a Interface continua a mostrar o dashboard. O utilizador nem repara.

3. **Escalabilidade granular**: Se amanha tivermos 1000 sensores em vez de 6, podemos duplicar apenas o Servidor (que e o gargalo de persistencia) sem precisar de escalar o Pre-Processamento ou a Analise. Numa arquitetura monolito, teriamos de escalar tudo junto, desperdicando recursos.

4. **Deploy e manutencao independentes**: Podemos atualizar o algoritmo de deteccao de anomalias na Analise (Python) sem redesenhar, recompilar ou redesenhar qualquer componente em C#. Isto e essencial quando a equipa de dados precisa de experimentar novos algoritmos sem afetar a equipa de infraestrutura.

5. **Testabilidade granular**: Podemos testar o Pre-Processamento com 25 testes Python sem precisar de arrancar o Gateway ou o Servidor. Isto torna os testes mais rapidos, mais especificos e mais faceis de automatizar.

6. **Aprendizagem academica**: O projeto e academico — implementar microservicos obriga a compreender comunicacao entre processos, serializacao, orquestracao e padroes de distribuicao que num monolito estariam ocultos.

### 2.2 Os 6 Microservicos do Projeto

```
┌─────────────────────────────────────────────────────────────────────┐
│                    MICROSERVICOS DO SISTEMA                         │
├──────────────┬────────────┬──────────┬──────────────────────────────┤
│ Servico      │ Linguagem  │ Porta    │ Responsabilidade             │
├──────────────┼────────────┼──────────┼──────────────────────────────┤
│ Sensor       │ C# .NET 9  │ -        │ Publica medicoes no RabbitMQ │
│ Gateway      │ C# .NET 9  │ -        │ Consome, valida, encaminha   │
│ Servidor     │ C# .NET 9  │ 7000/TCP │ Persiste dados, orquestra    │
│ PreProc      │ Python 3   │ 5001     │ Uniformiza e valida dados    │
│ Analise      │ Python 3   │ 6001     │ Estatisticas, anomalias      │
│ Interface    │ Python 3   │ 8000     │ Dashboard web                │
│ RabbitMQ     │ Erlang     │ 5672     │ Message broker               │
└──────────────┴────────────┴──────────┴──────────────────────────────┘
```

### 2.3 Docker e Orquestracao

**Docker** e uma plataforma de virtualizacao que permite empacotar cada servico com todas as suas dependencias num **container** isolado. O **Docker Compose** permite definir e gerir multiplas containers como um unico sistema.

**Porque Docker neste projeto?**

- **Consistencia**: O sistema corre da mesma forma em qualquer maquina (desenvolvimento, teste, producao)
- **Isolamento**: Cada servico tem o seu proprio filesystem, rede e processos
- **Rede interna**: Todos os servicos comunicam atraves de uma rede bridge (`sensor-network`) sem expor portas desnecessarias
- **Dependencias**: O Docker Compose garante que o RabbitMQ arranca antes do Gateway
- **Healthchecks**: Cada servico tem verificacao de saude automatica
- **Volumes**: Os dados persistem mesmo que os containers sejam reiniciados

**Porque Docker e nao execucao manual ou Kubernetes:**

| Abordagem | Porque nao foi escolhida |
|-----------|------------------------|
| **Execucao manual** | O projeto tem 7 processos (6 servicos + RabbitMQ). Arrancar cada um manualmente, com as portas corretas, variaveis de ambiente e ordem de dependencia, e propenso a erros e impossivel de reproduzir fiavelmente entre maquinas |
| **Kubernetes** | Exagerado para um projeto academico com 6 servicos. Kubernetes exige clusters, pods, services, ingress controllers e curva de aprendizagem enorme. Docker Compose resolve o mesmo problema com 1 ficheiro |
| **VMs tradicionais** | Pesadas (GBs cada), lentas de arrancar, difficult de partilhar entre membros da equipa. Containers partilham o kernel do host e arrancam em segundos |

**Porque Docker Compose especificamente:**
- **Reprodutibilidade**: `docker-compose up --build -d` arranca o sistema completo em qualquer maquina com Docker instalado — sem "funciona na minha maquina"
- **Dependencias automaticas**: O Docker Compose arranca o RabbitMQ antes do Gateway e Espera pelo healthcheck antes de iniciar o proximo servico
- **Rede interna automatica**: Todos os servicos comunicam por nomes (ex: `rabbitmq`, `pre-processamento`) sem configurar IPs
- **Logs centralizados**: `docker-compose logs` mostra logs de todos os servicos num unico sitio
- **Facilidade de demonstracao**: Num projeto academico, o professor pode avaliar o sistema com um unico comando

### 2.4 Comunicacao entre Microservicos

```
docker-compose.yml - Rede Interna:
┌─────────────────────────────────────────────────────────────┐
│                    sensor-network (bridge)                   │
│                                                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │
│  │ rabbitmq │  │pre-proc  │  │  analise │  │interface │   │
│  │  :5672   │  │  :5001   │  │  :6001   │  │  :8000   │   │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘   │
│                                                             │
│  ┌──────────┐  ┌──────────┐                                │
│  │ gateway  │  │ servidor │                                │
│  │  :7000*  │  │  :7000   │                                │
│  └──────────┘  └──────────┘                                │
│                                                             │
└─────────────────────────────────────────────────────────────┘

* Gateway nao expoe porta - e um client que liga ao Servidor
```

### 2.4 Comunicacao entre Microservicos

Neste projeto existem **3 paradigmas de comunicacao** diferentes, cada um escolhido para o caso de uso mais adequado:

```
┌─────────────────────────────────────────────────────────────┐
│               PARADIGMAS DE COMUNICACAO                     │
├─────────────────┬───────────────────┬───────────────────────┤
│ Paradigma       │ Protocolo         │ Quando Usar           │
├─────────────────┼───────────────────┼───────────────────────┤
│ Pub/Sub         │ AMQP (RabbitMQ)   │ Dados assincronos     │
│ Sincrono        │ TCP sockets       │ Stream persistente    │
│ Request/Response│ HTTP (RPC)        │ Operacoes com resposta│
└─────────────────┴───────────────────┴───────────────────────┘
```

---

## 3. RABBITMQ E O PROTOCOLO AMQP

### 3.1 O que e RabbitMQ?

RabbitMQ e um **message broker** (broker de mensagens) — um software que atua como intermediario entre quem envia mensagens (produtores) e quem as recebe (consumidores). Usa o protocolo **AMQP** (Advanced Message Queuing Protocol).

**Analogia simples:**
- Imagina um sistema de correios: tu deposits uma carta na caixa do correio (producer), o carteiro (broker) encaminha-a para o destinatario certo (consumer), e a carta fica guardada ate ser entregue (queue).
- Tu nao precisas de saber onde esta o destinatario — apenas depositas a mensagem e o broker trata de tudo.

### 3.2 Conceitos Fundamentais do AMQP

**AMQP** (Advanced Message Queuing Protocol) e um protocolo de comunicacao para mensagens orientado a filas. Foi projetado para:
- **Entrega fiavel**: Mensagens nao se perdem mesmo que o sistema falhe
- **Desacoplamento**: Produtores e consumidores nao precisam de estar ligados ao mesmo tempo
- **Routing flexivel**: Mensagens podem ser encaminhadas com base em regras

**Componentes principais do AMQP:**

```
┌─────────────────────────────────────────────────────────────┐
│                  COMPONENTES AMQP                           │
├──────────────┬──────────────────────────────────────────────┤
│ Producer     │ Quem envia a mensagem (Sensor)               │
│ Exchange     │ Recebe do producer e encaminha para queues    │
│ Queue        │ Fila onde as mensagens ficam armazenadas     │
│ Consumer     │ Quem recebe a mensagem (Gateway)             │
│ Binding      │ Regra que liga uma queue a um exchange        │
│ Routing Key  │ Chave usada para decidir que queue recebe     │
└──────────────┴──────────────────────────────────────────────┘
```

### 3.3 Tipos de Exchange

O RabbitMQ suporta varios tipos de exchange, cada um com uma estrategia de routing diferente:

```
┌──────────────┬──────────────────────────────────────────────────────┐
│ Tipo         │ Comportamento                                        │
├──────────────┼──────────────────────────────────────────────────────┤
│ Direct       │ Envia para queues cuja routing key e EXATAMENTE igual│
│ Fanout       │ Envia para TODAS as queues (ignora routing key)      │
│ Topic        │ Envia com base em padroes (wildcards) na routing key │
│ Headers      │ Envia com base nos headers da mensagem               │
└──────────────┴──────────────────────────────────────────────────────┘
```

**Neste projeto usamos Topic exchanges** porque permitem routing flexivel:

```
Routing keys suportadas pelo projeto:
  sensor.<sensor_id>.<tipo_dado>    → ex: sensor.sensor-01.temperatura
  zona.<zona>.<tipo_dado>          → ex: zona.A.temperatura

Wildcards suportados pelo tipo Topic:
  * (asterisco) = substitui EXATAMENTE uma palavra
  # (coringa)   = substitui ZERO ou mais palavras

Exemplos de binds no Gateway:
  sensor.*.#        → recebe todas as medicoes de qualquer sensor
  zona.*.#          → recebe todas as medicoes de qualquer zona
  sensor.01.*       → recebe apenas medicoes do sensor-01
  #                 → recebe tudo (todas as mensagens)
```

### 3.4 Exchanges e Queues no Projeto

```
EXCHANGE 1: sensor-measurements (Topic, durable)
│
├── Bind: gateway-measurements-{id} com key "sensor.*.#"
│   └── Recebe: TODAS as medicoes de TODOS os sensores
│
├── Bind: gateway-measurements-{id} com key "zona.*.#"
│   └── Recebe: medicoes agrupadas por zona geografica
│
EXCHANGE 2: sensor-control (Topic, durable)
│
├── Bind: gateway-control-{id} com key "#"
│   └── Recebe: registos, heartbeats, confirmacoes
│
└── Publicacoes no sensor-control:
    ├── routing key "register"      → Sensor regista-se
    ├── routing key "register_ok"   → Gateway confirma registo
    └── routing key "heartbeat"     → Sensor mantem keep-alive
```

### 3.5 Fluxo de Mensagens Detalhado

**1. Sensor publica uma medicao:**
```
Sensor                          RabbitMQ                       Gateway
  │                               │                              │
  │ 1. Conectar a RabbitMQ        │                              │
  ├──────────────────────────────►│                              │
  │                               │                              │
  │ 2. Declarar exchanges         │                              │
  │   (sensor-measurements)       │                              │
  ├──────────────────────────────►│                              │
  │                               │                              │
  │                               │  3. Gateway subscreve queue  │
  │                               │◄─────────────────────────────┤
  │                               │                              │
  │ 4. Publicar medicao           │                              │
  │   exchange: sensor-meas       │                              │
  │   routing: sensor.01.temp     │                              │
  ├──────────────────────────────►│                              │
  │                               │                              │
  │                               │  5. Entregar para queue      │
  │                               │     (routing key match)      │
  │                               ├─────────────────────────────►│
  │                               │                              │
  │                               │  6. ACK (mensagem recebida)  │
  │                               │◄─────────────────────────────┤
  │                               │                              │
  │                               │  7. Processar mensagem       │
  │                               │     (RPC PreProcessamento)   │
  │                               │                              │
  │                               │  8. Enviar ao Servidor (TCP) │
  │                               │                              │
```

### 3.6 Conceitos Avancados de RabbitMQ

**Delivery Modes:**
- `Non-persistent` (1): Mensagem fica apenas na RAM — se o RabbitMQ reiniciar, perde-se
- `Persistent` (2): Mensagem e gravada em disco — sobrevive a reinicios
- **Neste projeto usamos `Persistent`** porque queremos garantir que as medicoes nao se perdem

**Acknowledgements (ACK):**
- `autoAck: true`: O RabbitMQ considera a mensagem entregue logo que a envia ao consumer — arriscado
- `autoAck: false`: O consumer tem de enviar ACK explicitamente — seguro
- **Neste projeto usamos `autoAck: false`** e enviamos ACK manualmente apos processar

**Heartbeat (AMQP):**
- Mecanismo de keep-alive entre o cliente e o broker
- Se o broker nao receber um heartbeat dentro do tempo configurado, fecha a ligacao
- **Neste projeto: `RequestedHeartbeat = 30 segundos`**

**Automatic Recovery:**
- Se a ligacao ao broker cair, o cliente tenta reconectar automaticamente
- `AutomaticRecoveryEnabled = true` no `ConnectionFactory`

### 3.7 RabbitMQ vs Outros Brokers

| Caracteristica | RabbitMQ | Apache Kafka | ActiveMQ |
|---------------|----------|--------------|----------|
| Protocolo | AMQP 0-9-1 | Proprietario | STOMP/OpenWire |
| Modelo | Queue-based | Log-based | Queue-based |
| Persistencia | Disco + RAM | Disco | Disco + RAM |
| Routing | Exchanges flexiveis | Topics simples | Topics |
| Facilidade | Media | Complexa | Media |
| Performance | Alta | Muito alta | Media |
| **Escolha** | **Ideal para IoT** | Dados massivos | Legacy |

**Porque RabbitMQ e nao Kafka neste projeto:**
- Kafka e otimizado para streams de dados massivos (logs, eventos) — e exagerado para sensores IoT
- RabbitMQ e mais simples de configurar e gerir
- RabbitMQ tem routing mais flexivel (topic exchanges com wildcards)
- RabbitMQ e mais leve (ideal para ambiente Docker)

**Porque RabbitMQ e nao ActiveMQ:**
- ActiveMQ e mais orientado para enterprise Java — menos suporte nativo para C# e Python
- RabbitMQ tem melhor desempenho para mensagens pequenas e frequentes (como dados de sensores)
- RabbitMQ tem interface de gestao web integrada (porta 15672) — util para debug e monitorizacao
- RabbitMQ tem melhor suporte a Docker e ecossistema de containers

**Porque não usar filas diretamente (ex: Redis Lists, Amazon SQS)?**
- RabbitMQ suporta exchanges com routing por padroes (topic exchanges) — permits que o Gateway subscreva apenas mensagens de determinados sensores ou zonas
- Filas simples (como Redis Lists) nao tem routing inteligente — ou consumimos tudo ou precisamos de logica adicional no consumidor
- RabbitMQ tem acknowledgements (ACK) integrados — garante que mensagens nao se perdem se o Gateway falhar
- RabbitMQ tem persistencia em disco e durability de exchanges/queues — sobrevive a reinicios

---

## 4. SOCKETS TCP — COMUNICACAO DIRETA

### 4.1 O que e TCP?

TCP (Transmission Control Protocol) e um protocolo de transport que fornece uma **ligacao bidirecional, fiavel e orientada a stream** entre dois computadores.

**Caracteristicas fundamentais do TCP:**

```
┌─────────────────────────────────────────────────────────────┐
│                  CARACTERISTICAS TCP                        │
├──────────────────┬──────────────────────────────────────────┤
│ Orientado a      │ Os dados sao enviados como um fluxo      │
│ stream           │ continuo, nao como pacotes isolados       │
├──────────────────┼──────────────────────────────────────────┤
│ Fiavel           │ Garante que os dados chegam na ordem     │
│                  │ correta e sem duplicatas                  │
├──────────────────┼──────────────────────────────────────────┤
│ Bidirecional     │ Ambos os lados podem enviar e receber    │
│                  │ dados simultaneamente                     │
├──────────────────┼──────────────────────────────────────────┤
│ Controlo de      │ Ajusta a velocidade de envio consoante   │
│ fluxo            │ a capacidade do receptor                  │
├──────────────────┼──────────────────────────────────────────┤
│ Coneccao         │ Estabelece uma ligacao antes de enviar   │
│ orientada        │ dados (3-way handshake)                   │
└──────────────────┴──────────────────────────────────────────┘
```

### 4.2 TCP vs HTTP — Porque escolher TCP?

```
HTTP (usado pelo RPC):                TCP (usado pelo Gateway-Servidor):
┌──────────────────────┐              ┌──────────────────────┐
│ Request:             │              │ Ligacao persistente: │
│ GET /dados HTTP/1.1  │              │                      │
│ Host: servidor:7000  │              │ Abrir ligacao 1x     │
│ (headers...)         │              │ Enviar N mensagens   │
│                      │              │ Receber N respostas  │
│ Response:            │              │ Fechar ligacao       │
│ HTTP/1.1 200 OK      │              │                      │
│ Content-Type: ...    │              │ Sem headers HTTP     │
│ (headers...)         │              │ Sem overhead         │
│ {dados}              │              │ {dados}\n            │
└──────────────────────┘              └──────────────────────┘

Para cada medicao:                    Para todas as medicoes:
- Nova ligacao TCP                    - Mesma ligacao TCP
- 3-way handshake                     - Sem handshake
- Headers HTTP (~200 bytes)           - Sem headers
- Fechar ligacao                      - Stream continuo
```

**Porque TCP e melhor para Gateway→Servidor:**

1. **Baixa latencia**: Sem overhead de HTTP (nao ha headers HTTP a enviar por cada medicao)
2. **Eficiencia**: A ligacao e reutilizada para centenas/milhares de medicoes
3. **Simplicidade**: Estamos a enviar linhas JSON — nao precisamos de um servidor HTTP completo
4. **ACK imediato**: O Servidor responde com `DATA_ACK` pela mesma ligacao
5. **Multi-gateway**: O Servidor aceita multiplas ligacoes TCP simultaneas

**Porque nao usar HTTP/1.1 persistente (keep-alive)?**
- HTTP keep-alive mantem a ligacao aberta, mas continua com o overhead dos headers HTTP por cada request/response (~200 bytes por mensagem)
- Num fluxo de sensores onde cada medicao e pequena (~150 bytes JSON), os headers representam mais de 50% do trafego
- O HTTP precisa de parsing completo de headers, content-length, transfer-encoding, etc. — logica desnecessaria quando so queremos enviar JSON
- Com TCP puro, o parsing e trivial: le ate ao `\n` e parse o JSON

**Porque nao usar UDP?**
- UDP nao garante entrega — se um pacote se perder, a medicao perde-se sem aviso
- UDP nao garante ordem — mensagens podem chegar fora de sequencia
- Para dados de monitorizacao ambiental, a fiabilidade e mais importante que a velocidade minima que UDP oferece
- TCP resolve automaticamente retransmissoes e ordenacao — nao precisamos de implementar nada disso

**Porque nao usar WebSockets?**
- WebSockets foram pensados para comunicacao browser-servidor — neste caso Gateway e Servidor sao ambos servicos backend
- WebSockets adicionam uma camada de handshake HTTP upgrade e framing que e desnecessaria
- TCP puro e mais leve e mais direto para comunicacao entre servicos do mesmo sistema

### 4.3 O Protocolo Customizado sobre TCP

O projeto define um **protocolo proprio** (application layer protocol) sobre TCP:

```
┌─────────────────────────────────────────────────────────────┐
│              PROTOCOLO TCP DO PROJETO                       │
├─────────────────────────────────────────────────────────────┤
│ Formato: JSON + newline (\n)                               │
│ Tamanho maximo por linha: 10 MB                             │
│ Encoding: UTF-8                                             │
│ Delimitador de fim de mensagem: \n (0x0A)                   │
│                                                             │
│ Exemplo de envio (Gateway → Servidor):                     │
│ {"tipo":"DATA","sensor_id":"sensor-01",                    │
│  "payload":{"tipo_dado":"temperatura","valor":23.5},       │
│  "timestamp":"2025-06-05T10:30:00Z"}\n                     │
│                                                             │
│ Exemplo de resposta (Servidor → Gateway):                  │
│ {"tipo":"DATA_ACK","sensor_id":"sensor-01",                │
│  "payload":{},"timestamp":"2025-06-05T10:30:01Z"}\n        │
└─────────────────────────────────────────────────────────────┘
```

**Porque protocolo customizado JSON+newline e nao alternativas?**

| Alternativa | Porque nao foi escolhida |
|-------------|------------------------|
| **HTTP completo** | Headers HTTP (~200 bytes) sao overhead desnecessario quando o payload medio e ~150 bytes. Cada medicao teria mais bytes de headers que de dados uteis |
| **gRPC sobre HTTP/2** | Exige geracao de codigo a partir de ficheiros .proto, framework pesado para C# e Python, e adds complexidade de certificados TLS para comunicacao interna |
| **Protocolo binario proprio** | Mais rapido mas muito mais complexo de implementar, debugar e manter. JSON e legivel por humanos — util para debug em ambiente academico |
| **MessagePack / CBOR** | Serializacao binaria mais compacta que JSON, mas perde legibilidade. Para dados de sensores (poucos campos), a diferenca de tamanho e irrelevante |

**Porque JSON e nao binario:**
- **Legibilidade**: Em debug, podemos ver exatamente o que foi enviado com `netcat` ou `nc`
- **Simplicidade**: `System.Text.Json` (C#) e `json` (Python) fazem parse automatico — sem necessidade de schemas ou geracao de codigo
- **Interoperabilidade**: C# e Python comunicam sem problemas — nao ha problemas de endianness ou alinhamento
- **Compatibilidade**: Se amanha precisarmos de adicionar um campo, basta adicionar ao JSON — nao e necessario recompilar

**Porque delimitador `\n` (newline):**
- `StreamReader.ReadLine()` em C# e `readline()` em Python fazem parsing automatico ate ao `\n`
- Nao precisamos de enviar o tamanho da mensagem antecipadamente (como faríamos com protocolos binarios)
- `\n` e um caractere que nao aparece normalmente em JSON (e escapado como `\n` dentro de strings)
- Limite de 10MB previne mensagens malformadas de consumir memoria infinita

### 4.4 Tipos de Mensagem do Protocolo

```
┌────────────────┬──────────────────────────────────────────────┐
│ Tipo           │ Funcao                                        │
├────────────────┼──────────────────────────────────────────────┤
│ REGISTER       │ Sensor comunica ao Gateway que existe         │
│ REGISTER_OK    │ Gateway confirma que sensor foi aceite        │
│ REGISTER_ERR   │ Gateway rejeita o sensor                      │
│ DATA           │ Sensor envia uma medicao                      │
│ DATA_ACK       │ Servidor confirma que recebeu a medicao      │
│ HEARTBEAT      │ Sensor mantem ligacao ativa (keep-alive)     │
│ HEARTBEAT_ACK  │ Gateway confirma que recebeu o heartbeat     │
│ ERROR          │ Mensagem de erro genérico                     │
└────────────────┴──────────────────────────────────────────────┘
```

### 4.5 TCP Watchdog — Reconexao Automatica

O Gateway implementa um **watchdog** — uma thread em background que verifica periodicamente se a ligacao ao Servidor esta ativa:

```
┌─────────────────────────────────────────────────────────────┐
│               TCP WATCHDOG (30 segundos)                    │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  WatchdogWorker():                                         │
│  ┌──────────────────────────────────────────────────┐     │
│  │ 1. Verificar sensores inativos (>60s sem dados)  │     │
│  │ 2. Atualizar CSV se houver alteracoes             │     │
│  │ 3. Verificar ligacao ao Servidor                  │     │
│  │    - Se desconectado → ReconectarAsync()          │     │
│  │      - 20 tentativas maximo                       │     │
│  │      - 5 segundos entre tentativas                │     │
│  │      - Reconexao bem-sucedida → reiniciar ciclo   │     │
│  └──────────────────────────────────────────────────┘     │
│                                                             │
│  ReconectarAsync():                                        │
│  ┌──────────────────────────────────────────────────┐     │
│  │ 1. Criar novo TcpClient                          │     │
│  │ 2. Tentar Connect(ip, port)                       │     │
│  │ 3. Se sucesso:                                    │     │
│  │    - Fechar ligacao antiga                        │     │
│  │    - Substituir streams                           │     │
│  │    - Atualizar flag isServerConnected             │     │
│  │ 4. Se falha: aguardar 5s e tentar novamente      │     │
│  └──────────────────────────────────────────────────┘     │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 4.6 Multithreading e Concorrencia

O Servidor TCP usa **multithreading** para aceitar varias Gateways simultaneamente:

```
Servidor (porta 7000):
┌─────────────────────────────────────────────────────────────┐
│  TcpListener.AcceptTcpClient()                             │
│       │                                                     │
│       ├── Gateway #1 conectou → Thread 1 (HandleGateway)   │
│       ├── Gateway #2 conectou → Thread 2 (HandleGateway)   │
│       └── Gateway #3 conectou → Thread 3 (HandleGateway)   │
│                                                             │
│  Cada Thread:                                               │
│  - Le mensagens JSON com reader.ReadLine()                  │
│  - Processa DATA, REGISTER, etc.                           │
│  - Envia ACK pelo mesmo stream                             │
│  - Trata desligamento com cleanup                           │
│                                                             │
│  Protecao contra concorrencia:                              │
│  - Mutex para gatewayCount e gatewayMap                     │
│  - Mutex por tipo de dado (ServidorMonitor)                │
│  - lock para gatewayMessages                               │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. CHAMADAS RPC — REMOTE PROCEDURE CALL

### 5.1 O que e RPC?

RPC (Remote Procedure Call) e um paradigma de comunicacao que permite **chamar uma funcao num servidor remoto como se fosse uma funcao local**. O programador faz uma chamada de funcao normal, e o sistema trata de:
1. Serializar os argumentos da funcao
2. Enviar para o servidor remoto
3. O servidor executa a funcao
4. Serializa o resultado
5. Devolve ao cliente

**Analogia:**
- E como fazer uma chamada de telefone: tu ligas (request), a pessoa responde (executa a funcao), e diz-te o resultado (response). Nao precisas de saber onde a pessoa esta — apenas ligas e ouves a resposta.

### 5.2 RPC HTTP vs RPC gRPC

Existem duas implementacoes principais de RPC:

```
┌──────────────────────┬──────────────────────────────────────┐
│ RPC HTTP (JSON)      │ RPC gRPC (Protobuf)                  │
├──────────────────────┼──────────────────────────────────────┤
│ Transport: HTTP/1.1  │ Transport: HTTP/2                    │
│ Formato: JSON texto  │ Formato: Protobuf binario            │
│ Velocidade: Media    │ Velocidade: Alta                      │
│ Legibilidade: Alta   │ Legibilidade: Baixa (binario)        │
│ Contrato: Fraco      │ Contrato: Forte (.proto)             │
│ Streaming: Nao       │ Streaming: Bidirecional              │
│ Simplicidade: Alta   │ Simplicidade: Complexa               │
├──────────────────────┼──────────────────────────────────────┤
│ **Neste projeto**    │ **Definido em proto/ mas nao usado** │
│ JSON-RPC sobre HTTP  │ Proto files desatualizados           │
└──────────────────────┴──────────────────────────────────────┘
```

**Porque RPC HTTP/JSON e nao gRPC:**

1. **Simplicidade de implementacao**: HTTP/JSON e trivial em ambas as linguagens — `HttpClient` em C# e `requests` em Python. gRPC exige geracao de codigo a partir de ficheiros .proto, configuracao de canais, e frameworks especificos (`Grpc.Net.Client` em C#, `grpcio` em Python)

2. **Debug e inspecao**: Com HTTP/JSON podemos usar ferramentas universais como `curl`, Postman ou browser para testar endpoints. gRPC usa HTTP/2 binario — precisamos de ferramentas especializadas como `grpcurl`

3. **Legibilidade dos logs**: Quando o sistema falha, podemos ver exatamente o que foi enviado/recebido nos logs. Com Protobuf binario, os logs seriam hexadecimais ilegiveis

4. **Prototipagem rapida**: Num projeto academico, precisamos de testar ideias rapidamente. HTTP/JSON permite alterar o payload sem recompilar — basta mudar o JSON. gRPC exige alterar o .proto, regerar o codigo, e recompilar ambos os lados

5. **Ecossistema de testes**: O pytest (Python) e xUnit (C#) funcionam perfeitamente com HTTP. Testes de integracao com gRPC sao mais complexos de configurar

6. **Ferramentas existentes**: O projeto ja usa `System.Text.Json` em C# e `json` em Python — nao precisamos de adicionar dependencias novas

**Quando gRPC seria preferivel:**
- Se tivessemos milhares de chamadas RPC por segundo (performance critica)
- Se precisassemos de streaming bidirecional (ex: sensor a enviar dados em tempo real via RPC)
- Se o contrato entre servicos fosse muito complexo e precisassemos de tipagem forte

### 5.3 RPC no Projeto — Onde e Como

**Fluxo RPC do Gateway para Pre-Processamento:**

```
Gateway                              Pre-Processamento (Python)
  │                                          │
  │ 1. Serializar argumentos para JSON       │
  │    {sensor_id, tipo_dado, valor, ...}    │
  │                                          │
  │ 2. POST http://pre-processamento:5001    │
  │    /rpc/uniformizar                      │
  │    Content-Type: application/json        │
  ├─────────────────────────────────────────►│
  │                                          │
  │                                  3. Processar:
  │                                     - Converter unidades
  │                                     - Validar limites
  │                                     - Formatar timestamp
  │                                          │
  │  4. Response JSON                        │
  │  {sucesso, valor_uniformizado, unidade}  │
  │◄─────────────────────────────────────────┤
  │                                          │
  │  5. Se falhar → retry (2x, delay 1s/2s) │
```

**Fluxo RPC do Servidor para Analise:**

```
Servidor                                Analise (Python)
  │                                          │
  │ 1. Serializar valores para JSON          │
  │    {sensor_id, tipo_dado, valores[]}     │
  │                                          │
  │ 2. POST http://analise:6001              │
  │    /rpc/estatisticas                     │
  │    Content-Type: application/json        │
  ├─────────────────────────────────────────►│
  │                                          │
  │                                  3. Calcular:
  │                                     - Media, mediana
  │                                     - Desvio padrao
  │                                     - Minimo, maximo
  │                                          │
  │  4. Response JSON                        │
  │  {sucesso, media, mediana, desvio, ...}  │
  │◄─────────────────────────────────────────┤
  │                                          │
  │  5. Se falhar → retry (2x, delay 1s/2s) │
```

### 5.4 Endpoints RPC Implementados

| Rota | Metodo HTTP | Funcao | Parametros | Retorno |
|------|-------------|--------|------------|---------|
| `/rpc/uniformizar` | POST | Uniformizar valor | sensor_id, tipo_dado, valor, timestamp | Sucesso, ValorUniformizado, Unidade |
| `/rpc/validar` | POST | Validar dados | sensor_id, tipo_dado, valor | Valido, Erros[] |
| `/rpc/estatisticas` | POST | Calcular estatisticas | sensor_id, tipo_dado, valores[] | Media, Mediana, Desvio, Min, Max, Q1, Q3 |
| `/rpc/padroes` | POST | Detetar padroes | sensor_id, tipo_dado, valores[] | Anomalias[], Tendencia, TotalAnomalias |
| `/rpc/previsao` | POST | Prever riscos | sensor_id, tipo_dado, valores[] | ProximoValor, Previsoes[], Tendencia, Risco |

### 5.5 Retry com Backoff Exponencial

Ambos os clientes RPC implementam retry automatico com **backoff exponencial**:

```
Tentativa 1: Falha → aguardar 1 segundo
Tentativa 2: Falha → aguardar 2 segundos
Tentativa 3: Falha → desistir (retornar null)

Código-fonte (PreProcessamentoClient.cs):
┌─────────────────────────────────────────────────────────────┐
│  private static async Task<T?> ComRetryAsync<T>(           │
│      Func<Task<T?>> action, int maxRetries = 2)           │
│  {                                                          │
│      for (int i = 0; ; i++)                                │
│      {                                                      │
│          try { return await action(); }                     │
│          catch (Exception ex) when (i < maxRetries)        │
│          {                                                  │
│              Console.WriteLine($"RPC falhou (tentativa    │
│                  {i + 1}/{maxRetries}): {ex.Message}");   │
│              await Task.Delay(1000 * (i + 1));  // Backoff │
│          }                                                  │
│          catch (Exception ex) { return null; }              │
│      }                                                      │
│  }                                                          │
└─────────────────────────────────────────────────────────────┘
```

**Porque backoff e nao retry imediato:**
- Se o servico estiver sobrecarregado, retry imediato so piora o problema
- Backoff da tempo ao servico para recuperar
- Delay progressivo (1s, 2s) reduz pressao sobre o servico

---

## 6. GRPC E PROTOCOL BUFFERS

### 6.1 O que e gRPC?

gRPC (Google Remote Procedure Call) e um framework de RPC de alta performance desenvolvido pelo Google. Usa **Protocol Buffers** para serializacao binaria e **HTTP/2** como transport.

### 6.2 Protocol Buffers (Protobuf)

Protobuf e um formato de serializacao binaria — os dados sao convertidos para bytes binarios em vez de texto JSON:

```
JSON (texto):                          Protobuf (binario):
┌──────────────────────────┐          ┌──────────────────────────┐
│ {                        │          │ 08 01 12 08 73 65 6E 73  │
│   "tipo": "DATA",        │          │ 6F 72 2D 30 31 1A 0D 74  │
│   "sensor_id": "sensor-  │          │ 65 6D 70 65 72 61 74 75  │
│     01",                  │          │ 72 61 20 32 33 2E 35     │
│   "payload": {           │          │                          │
│     "tipo_dado":         │          │ ~20 bytes vs ~120 bytes   │
│       "temperatura",     │          │ 6x mais compacto          │
│     "valor": 23.5        │          │ 10x mais rapido           │
│   }                      │          │                          │
│ }                        │          └──────────────────────────┘
└──────────────────────────┘
```

### 6.3 Ficheiros .proto do Projeto

Os ficheiros `.proto` estao definidos em `proto/` mas estao **desatualizados** — a implementacao atual usa JSON-RPC sobre HTTP:

```
proto/
├── analise.proto              ← Marcado como desatualizado
│   // NOTA: Este ficheiro .proto está desatualizado.
│   // A implementação real usa JSON-RPC sobre HTTP (não gRPC).
│   // Ver src/Analise/servico.py e src/Servidor/AnaliseClient.cs
│   // Mantido apenas como referência de desenho da interface RPC.
│
└── pre_processamento.proto    ← Marcado como desatualizado
    // NOTA: Este ficheiro .proto está desatualizado.
    // A implementação real usa JSON-RPC sobre HTTP (não gRPC).
    // Ver src/PreProcessamento/servico.py e src/Gateway/PreProcessamentoClient.cs
    // Mantido apenas como referência de desenho da interface RPC.
```

### 6.4 Porque gRPC seria uma Evolucao Natural

| Aspeto | HTTP/JSON (atual) | gRPC (futuro) |
|--------|-------------------|---------------|
| Serializacao | JSON (texto) | Protobuf (binario) |
| Transport | HTTP/1.1 | HTTP/2 |
| Velocidade | Media | 10x mais rapido |
| Tamanho payload | ~120 bytes | ~20 bytes |
| Streaming | Nao suportado | Bidirecional |
| Contrato | Implicito (JSON) | Explicito (.proto) |
| Tipagem | Runtime | Compile-time |

**Porque gRPC seria uma evolucao natural e nao uma substituicao imediata:**

A transicao de HTTP/JSON para gRPC faz sentido como evolucao, nao como substituicao, por estas razoes:

1. **Os ficheiros .proto ja existem**: O projeto ja tem `analise.proto` e `pre_processamento.proto` definidos — apenas estao desatualizados. Atualiza-los e mais rapido que criar do zero

2. **A interface RPC e estavel**: Os endpoints `/rpc/estatisticas`, `/rpc/padroes`, `/rpc/previsao` e `/rpc/uniformizar` ja estao definidos e a funcionar. gRPC apenas mudaria a transport e serializacao, nao a logica de negocio

3. **Beneficio real so aparece em escala**: Com 6 sensores, a diferenca entre JSON (~120 bytes) e Protobuf (~20 bytes) e irrelevante. Se o sistema crescer para 1000+ sensores, a economia de 100 bytes por mensagem x 1000 mensagens/segundo = 100KB/segundo de trafego poupado

4. **Streaming bidirecional e o verdadeiro ganho**: A funcionalidade mais valiosa de gRPC e o streaming — o Sensor podia enviar dados continuamente sem abrir nova ligacao HTTP por cada medicao. Isto eliminaria a necessidade de TCP customizado para o fluxo Sensor→Gateway

5. **Contrato forte evita bugs**: Com gRPC, se o Sensor enviar um campo errado, o erro e detetado em compilacao. Com JSON, so detetamos em runtime (ex: `TypeError: cannot read property 'valor' of undefined`)

6. **O caminho de migracao e claro**: A ordem natural seria: HTTP/JSON (atual) → gRPC para RPCs internas → gRPC para Sensor→Gateway → eventualmente eliminar TCP customizado

**Porque nao migrar agora:**
- O projeto esta funcional com HTTP/JSON — "if it ain't broke, don't fix it"
- O overhead de aprendizagem de gRPC para a equipa nao justifica os ganhos com 6 sensores
- O debug com HTTP/JSON e mais rapido durante o desenvolvimento

---

## 7. STACK TECNOLOGICA COMPLETA

### 7.1 C# .NET 9.0

**Onde e usado:** Sensor, DataStreamClient, Gateway, Servidor, SharedProtocol

**Porque C#:**
- **Performance**: Mais rapido que Python para operacoes de rede e processamento
- **Tipagem forte**: Erros detetados em compilacao, menos bugs em producao
- **Async/Await**: Suporte nativo para programacao assincrona
- **Bibliotecas**: RabbitMQ.Client, System.Net.Sockets, System.Text.Json

**Porque C# para estes componentes especificamente e nao outra linguagem:**

| Componente | Porque C# e a melhor escolha |
|------------|------------------------------|
| **Sensor** | Precisa de performance para publicar medicoes continuamente. C# com `RabbitMQ.Client` e mais rapido que Python com `pika`. Tambem precisa de `System.Text.Json` para serializacao rapida |
| **Gateway** | E o componente mais critico — se falhar, todos os dados se perdem. C# com `HttpClient` e `TcpClient` oferece gestao de erros robusta, timeouts configuraveis, e async/await nativo para nao bloquear enquanto espera de respostas RPC |
| **Servidor** | Precisa de multithreading para aceitar multiplas Gateways. C# com `TcpListener` e `Thread` e mais eficiente que Python com `socket` (GIL limita concorrencia real) |
| **SharedProtocol** | Biblioteca partilhada entre todos os componentes C#. C# permite criar um assembly (.dll) que e referenciado por todos — Python nao tem este conceito de biblioteca compilada partilhada |

**Porque nao Java:**
- Java e mais pesado (JVM) e mais lento de desenvolver que C# para este tipo de projeto
- .NET 9.0 e mais moderno que Java para programacao assincrona
- O ecossistema NuGet (C#) e mais focado em redes e web que o Maven (Java)

**Porque nao Go:**
- Go e excelente para servidores de rede, mas a curva de aprendizagem e maior
- O ecossistema de bibliotecas para IoT e menos maduro que o de C#
- Go nao tem o conceito de "solution" que facilita a organizacao do projeto no Visual Studio

**Componentes .NET:**

```
┌─────────────────────────────────────────────────────────────┐
│                  PROJETOS .NET                              │
├──────────────────┬──────────────────────────────────────────┤
│ SharedProtocol   │ Biblioteca partilhada                    │
│                  │ - Mensagem.cs (modelo de dados)          │
│                  │ - MensagemSerializer.cs (JSON)           │
│                  │ - TiposMensagem.cs (constantes)          │
│                  │ - CodigosErro.cs (codigos de erro)       │
│                  │ - LogHelper.cs (logging com rotacao)     │
├──────────────────┼──────────────────────────────────────────┤
│ Sensor           │ Publica medicoes no RabbitMQ              │
│                  │ - RabbitMQSensorClient.cs                │
│                  │ - Program.cs (menu interativo)           │
├──────────────────┼──────────────────────────────────────────┤
│ Gateway          │ Consome, valida, encaminha               │
│                  │ - RabbitMQGatewayClient.cs               │
│                  │ - PreProcessamentoClient.cs (RPC)        │
│                  │ - Program.cs (logica principal)          │
│                  │ - SensorInfo.cs (modelo)                 │
├──────────────────┼──────────────────────────────────────────┤
│ Servidor         │ Persiste dados, orquestra analise        │
│                  │ - Program.cs (TCP server)                │
│                  │ - ServidorMonitor.cs (SQLite)            │
│                  │ - AnaliseClient.cs (RPC)                 │
└──────────────────┴──────────────────────────────────────────┘
```

### 7.2 Python 3

**Onde e usado:** Pre-Processamento, Analise, Interface

**Porque Python:**
- **Rapidez de desenvolvimento**: Codigo mais curto e expressivo
- **Bibliotecas cientificas**: numpy, scipy para estatisticas e deteccao de anomalias
- **Web frameworks**: Flask/FastAPI para interfaces REST
- **Simplicidade**: Facil de implementar servicos RPC rapidamente

**Porque Python para estes componentes especificamente e nao outra linguagem:**

| Componente | Porque Python e a melhor escolha |
|------------|----------------------------------|
| **Pre-Processamento** | A logica e simples (conversao de unidades, validacao de limites) — Python permite implementar em 50 linhas o que em C# precisaria de 150. Flask e trivial para expor um endpoint RPC |
| **Analise** | Calculo estatistico e deteccao de anomalias — numpy e scipy sao as bibliotecas de referencia mondial. Em C# precisariamos de Math.NET (menos maduro) ou implementar do zero |
| **Interface** | Dashboard web simples — Flask/FastAPI com Jinja2 templates. Python e mais rapido para prototipar UIs web que ASP.NET |

**Porque nao R para Analise:**
- R e especializado em estatistica, mas e mau para servidores web e APIs
- Python e mais generalista — podemos usar para analise, web, e testes
- O ecossistema de machine learning (scikit-learn, pandas) e mais maduro em Python
- A equipa ja conhece Python — R exigiria aprendizagem adicional

**Porque nao Node.js para Interface:**
- Node.js e bom para I/O assincrono, mas nao tem as bibliotecas cientificas que precisamos
- Python com Flask e mais simples para prototipar que Express.js
- O projeto ja usa Python para Pre-Processamento e Analise — manter tudo em Python reduz a complexidade

**A estrategia de linguagens mistas (C# + Python):**
A escolha de usar C# para infraestrutura e Python para ciencia nao e aleatoria — reflete a especializacao natural:
- **C#** e excellent para operacoes de rede, multithreading, e IO — exactamente o que Sensor, Gateway e Servidor precisam
- **Python** e excellent para processamento de dados, estatistica, e web scraping — exactamente o que Pre-Processamento, Analise e Interface precisam
- A comunicacao entre as duas linguagens e feita via HTTP/JSON — o "lingua franca" que ambos entendem perfeitamente

**Servicos Python:**

```
┌─────────────────────────────────────────────────────────────┐
│                  SERVICOS PYTHON                            │
├──────────────────┬──────────────────────────────────────────┤
│ Pre-Processamento│ Uniformizacao e validacao de dados       │
│ (porta 5001)     │ - servico.py (endpoints RPC)            │
│                  │ - conversao de unidades                  │
│                  │ - validacao de limites                   │
├──────────────────┼──────────────────────────────────────────┤
│ Analise          │ Estatisticas e deteccao de padroes       │
│ (porta 6001)     │ - servico.py (endpoints RPC)            │
│                  │ - analise_estatistica.py                 │
│                  │ - detecao_padroes.py                     │
├──────────────────┼──────────────────────────────────────────┤
│ Interface        │ Dashboard web                            │
│ (porta 8000)     │ - main.py (servidor web)                │
│                  │ - static/index.html (HTML)               │
│                  │ - static/script.js (JavaScript)          │
│                  │ - static/style.css (CSS)                 │
└──────────────────┴──────────────────────────────────────────┘
```

### 7.3 SQLite

**Onde e usado:** Persistencia de medicoes e analises no Servidor

**O que e SQLite:**
- Base de dados **embutida** (embedded) — corre dentro da mesma aplicacao
- Sem servidor dedicado — os dados ficam num unico ficheiro `.db`
- SQL completo e padrao
- Ideal para projetos academicos e prototipos

**Vantagens neste projeto:**
- **Simplicidade**: Sem necessidade de configurar um servidor de BD
- **Portabilidade**: O ficheiro `sistemas_distribuidos.db` pode ser copiado facilmente
- **Performance**: Leitura/escrita rapida para medicoes
- **Concorrencia**: Suporta multiplos leitores simultaneos

```
dados/sistemas_distribuidos.db:
┌─────────────────────────────────────────────────────────────┐
│  Tabelas principais:                                        │
│                                                             │
│  medicoes                                                   │
│  ├── id INTEGER PRIMARY KEY                                 │
│  ├── sensor_id TEXT                                         │
│  ├── tipo_dado TEXT                                         │
│  ├── valor TEXT                                             │
│  ├── timestamp TEXT                                         │
│  └── payload_json TEXT (opcional)                           │
│                                                             │
│  analises                                                   │
│  ├── id INTEGER PRIMARY KEY                                 │
│  ├── sensor_id TEXT                                         │
│  ├── tipo_dado TEXT                                         │
│  ├── tipo_analise TEXT (estatisticas/padroes/previsao)      │
│  ├── resultado_json TEXT                                    │
│  └── timestamp TEXT                                         │
└─────────────────────────────────────────────────────────────┘
```

**Porque SQLite e nao PostgreSQL, MySQL ou MongoDB:**

| BD | Porque nao foi escolhida |
|----|------------------------|
| **PostgreSQL** | Exige um container separado, configuracao de utilizadores/permissoes, e e excessivo para um projeto academico com poucos dados. A complexidade de gestao (backups, upgrades, logs) nao justifica as vantagens para 6 sensores |
| **MySQL** | Semelhante ao PostgreSQL — exige servidor dedicado, configuracao de charset, e e mais complexo de configurar em Docker. MySQL e mais lido que SQLite para writes frequentes de sensores |
| **MongoDB** | NoSQL e bom para dados nao estruturados, mas os nossos dados sao estruturados (medicoes com schema fixo). MongoDB exige container separado e e mais pesado em memoria |
| **Redis** | Redis e uma cache — nao e indicado para persistencia permanente. Se o container Redis cair, os dados perdem-se |
| **Filesystem (CSV/JSON)** | Ja usamos CSV para o Gateway, mas nao suporta queries complexas (ex: "medicoes dos ultimos 5 minutos"). SQLite resolve isto com SQL padrao |

**Porque SQLite funciona neste contexto:**
- **Volume de dados**: 6 sensores x 1 medicao/5s = ~1000 medicoes/hora. SQLite aguenta facilmente 100K+ writes/segundo
- **Concorrencia**: O Servidor e o unico que escreve — nao ha concorrencia de writes. A Interface le — SQLite suporta multiplos leitores simultaneos
- **Integridade**: ACID completo — mesmo que o container caia a meio de uma escrita, a BD nao corrompe
- **Zero configuracao**: Nao precisa de passwords, portas, ou ligacoes de rede. O ficheiro `.db` e auto-contido
- **Portabilidade**: O ficheiro da BD pode ser copiado, analisado com ferramentas externas (DB Browser for SQLite), ou partilhado entre investigadores

### 7.4 Mensagens Serializadas — SharedProtocol

O projeto define uma **biblioteca partilhada** (`SharedProtocol`) usada por todos os componentes C# para garantir compatibilidade nas mensagens:

```csharp
// Mensagem.cs — Modelo de dados
public class Mensagem
{
    public string Tipo { get; init; }           // "DATA", "REGISTER", etc.
    public string SensorId { get; init; }       // "sensor-01"
    public Dictionary<string, object> Payload { get; init; }  // dados variaveis
    public string Timestamp { get; init; }      // ISO 8601
}
```

**Serializacao JSON:**
```
Mensagem.CriarData("sensor-01", "temperatura", 23.5)
    ↓ Serializar
{"tipo":"DATA","sensor_id":"sensor-01",
 "payload":{"tipo_dado":"temperatura","valor":23.5},
 "timestamp":"2025-06-05T10:30:00Z"}
    ↓ Enviar via TCP/RabbitMQ
{"tipo":"DATA_ACK","sensor_id":"sensor-01","payload":{},"timestamp":"..."}
```

**Porque SharedProtocol como biblioteca partilhada e nao copiar codigo:**

1. **Consistencia**: Se amanha alterarmos o formato da Mensagem (ex: adicionar campo `versao`), so alteramos `Mensagem.cs` e todos os componentes (Sensor, Gateway, Servidor) recebem a atualizacao automaticamente ao recompilar

2. **DRY (Don't Repeat Yourself)**: Sem SharedProtocol, teriamos de copiar a classe `Mensagem` e `MensagemSerializer` para cada projeto — se esquecessemos de atualizar um, teriamos bugs de serializacao/deserializacao silenciosos

3. **Tipagem compartilhada**: O `TipoMensagem` (constantes como "DATA", "REGISTER", "HEARTBEAT") e o `CodigoErro` estao definidos uma vez e usados por todos — evita "magic strings" espalhadas pelo codigo

4. **Testabilidade**: Podemos testar a serializacao/deserializacao uma vez no SharedProtocol, e saber que todos os componentes usam a mesma logica

5. **Organizacao do projeto**: SharedProtocol e um projeto .NET separado na solution — facilita a gestao no Visual Studio e a referencia entre projetos

**Porque nao usar NuGet packages para partilhar:**
- O SharedProtocol e especifico deste projeto — nao faz sentido publicar como pacote
- Referenciar diretamente o projeto (.csproj reference) e mais simples que configurar um feed NuGet privado
- Permite desenvolvimento e debug em tempo real — alteracoes no SharedProtocol refletem-se imediatamente

---

## 8. FLUXO DE DADOS COMPLETO

### 8.1 Fluxo Passo a Passo

```
┌─────────────────────────────────────────────────────────────┐
│                   FLUXO COMPLETO                            │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│  1. SENSOR PUBLICA MEDICAO                                  │
│     └─ Formato: sensor.<id>.<tipo>                         │
│     └─ Valor: JSON com {tipo, sensor_id, payload, ts}      │
│     └─ Exchange: sensor-measurements (Topic)                │
│                                                             │
│  2. RABBITMQ ENCAMINHA                                     │
│     └─ Routing key match: sensor.*.#                        │
│     └─ Entrega para queue: gateway-measurements-{id}        │
│     └─ Gateway consome a mensagem                          │
│                                                             │
│  3. GATEWAY PROCESSA                                       │
│     └─ Verifica se sensor esta registado e ativo           │
│     └─ Invoca RPC PreProcessamento:                        │
│        - POST /rpc/uniformizar (normalizar valor)           │
│        - POST /rpc/validar (verificar limites)             │
│     └─ Se invalido → descarta medicao                      │
│                                                             │
│  4. GATEWAY ENVIA AO SERVIDOR                              │
│     └─ Formato: JSON + \n via TCP socket                   │
│     └─ Aguarda DATA_ACK (timeout 5s)                       │
│     └─ Se falha → tenta reconexao                          │
│                                                             │
│  5. SERVIDOR PERSISTE EM SQLITE                            │
│     └─ Grava na tabela medicoes                            │
│     └─ Envia DATA_ACK ao Gateway                           │
│     └─ Se necessario, invoca Analise via RPC:              │
│        - POST /rpc/estatisticas                            │
│        - POST /rpc/padroes                                 │
│        - POST /rpc/previsao                                │
│                                                             │
│  6. INTERFACE WEB CONSULTA                                 │
│     └─ Le dados do SQLite                                  │
│     └─ Mostra dashboard em tempo real                      │
│     └─ Atualiza automaticamente (polling)                  │
│                                                             │
└─────────────────────────────────────────────────────────────┘
```

### 8.2 Diagrama de Sequencia Completo

```
Sensor    RabbitMQ    Gateway    PreProc    Servidor    Analise    Interface
  │          │           │          │          │           │           │
  │ 1.PUB    │           │          │          │           │           │
  ├─────────►│           │          │          │           │           │
  │          │ 2.DELIVER │          │          │           │           │
  │          ├──────────►│          │          │           │           │
  │          │           │          │          │           │           │
  │          │           │ 3.RPC    │          │           │           │
  │          │           ├─────────►│          │           │           │
  │          │           │          │          │           │           │
  │          │           │ 4.RESP   │          │           │           │
  │          │           │◄─────────┤          │           │           │
  │          │           │          │          │           │           │
  │          │           │ 5.RPC    │          │           │           │
  │          │           ├─────────►│          │           │           │
  │          │           │          │          │           │           │
  │          │           │ 6.RESP   │          │           │           │
  │          │           │◄─────────┤          │           │           │
  │          │           │          │          │           │           │
  │          │           │ 7.TCP    │          │           │           │
  │          │           ├─────────►│          │           │           │
  │          │           │          │          │           │           │
  │          │           │ 8.ACK    │          │           │           │
  │          │           │◄─────────┤          │           │           │
  │          │           │          │          │           │           │
  │          │           │          │ 9.PERSIST│           │           │
  │          │           │          ├─────────►│           │           │
  │          │           │          │          │           │           │
  │          │           │          │          │ 10.RPC    │           │
  │          │           │          │          ├──────────►│           │
  │          │           │          │          │           │           │
  │          │           │          │          │ 11.RESP   │           │
  │          │           │          │          │◄──────────┤           │
  │          │           │          │          │           │           │
  │          │           │          │          │ 12.QUERY  │           │
  │          │           │          │          ├──────────────────────►│
  │          │           │          │          │           │           │
```

---

## 9. COMO EXECUTAR

### 9.1 Tudo em Docker (Recomendado)

```bash
# Arrancar todos os 6 servicos
docker-compose up --build -d

# Verificar status
docker-compose ps

# Ver logs do Gateway
docker-compose logs -f gateway

# Parar tudo
docker-compose down
```

**Portas expostas:**
- `http://localhost:8000` — Dashboard web
- `http://localhost:15672` — RabbitMQ Management UI (guest/guest)
- `localhost:7000` — Servidor TCP

### 9.2 Sensores Simulados (Python)

```bash
# Arrancar 6 sensores virtuais (intervalo padrao: 5s)
python scripts/simular_sensores.py

# Com intervalo personalizado (2 segundos entre medicoes)
python scripts/simular_sensores.py 2
```

**Sensores virtuais:**
- `sensor-temperatura-01` (zona A): temperatura 15-35°C, humidade 30-90%
- `sensor-qualidade-01` (zona B): qualidade_ar 0-500, pm25 0-250, pm10 0-430
- `sensor-ruido-01` (zona A): ruido 30-100 dB
- `sensor-luz-01` (zona C): luminosidade 0-100000 lux
- `sensor-ambiente-01` (zona B): temperatura, humidade, qualidade_ar
- `sensor-particulas-01` (zona C): pm25, pm10, ruido

### 9.3 Sensor Individual (C#)

```bash
cd src/Sensor
dotnet run sensor-01 localhost 5672
```

### 9.4 Testes

```bash
# Testes C# (14 testes)
dotnet test tests/Gateway.Tests/Gateway.Tests.csproj
dotnet test tests/Servidor.Tests/Servidor.Tests.csproj

# Testes Python (25 testes)
cd tests/Analise.Tests
python -m pytest -v
```

---

## 10. CONCEITOS AVANCADOS

### 10.1 Padrao Publisher-Subscriber

O padrao Pub/Sub desacopla produtores de consumidores:
- **Producer**: Envia mensagem para um topico/exchange
- **Subscriber**: Subscreve um topico e recebe mensagens
- **Broker**: Encaminha mensagens (RabbitMQ)

**Vantagens:**
- Multiplos consumidores podem receber a mesma mensagem
- Nenhum consumidor precisa de saber dos outros
- O producer nao precisa de saber quantos consumidores existem

**Porque Pub/Sub e nao Request-Response para Sensor→Gateway:**

1. **Desacoplamento temporal**: O Sensor nao precisa de saber se o Gateway esta disponivel. Se o Gateway estiver offline, o RabbitMQ guarda a mensagem (persistente) e entrega quando ele voltar. Com Request-Response, o Sensor teria de esperar ou falhar

2. **Escalabilidade**: Se amanha tivermos 2 Gateways (load balancing), o Sensor nao precisa de mudar — ambas recebem as mesmas mensagens via topic exchange. Com Request-Response, teriamos de configurar o Sensor para enviar para 2 endpoints

3. **Um-para-muitos**: Uma medicao pode ser consumida pelo Gateway (para processamento), por um servico de auditoria (para logs), e por um servico de alertas (para detetar anomalias em tempo real). Pub/Sub permite isto naturalmente com bindings differentes

4. **Resiliencia**: Se o Gateway falhar a processar uma mensagem, o RabbitMQ pode reenvia-la para outro consumer (se houver). Com Request-Response, a mensagem perde-se

**Porque nao usar Pub/Sub para tudo:**
- Pub/Sub nao tem garantia de resposta — o Sensor nao sabe se a medicao foi processada
- Para operacoes que precisam de resposta (ex: "valida este dado"), Request-Response e mais natural
- O projeto usa ambos: Pub/Sub para dados (Sensor→Gateway) e Request-Response para operacoes (Gateway→PreProc, Servidor→Analise)

### 10.2 Padrao Request-Response sobre TCP

O projeto implementa um padrao Request-Response proprio sobre TCP:
1. Gateway envia JSON + `\n` (request)
2. Servidor processa e responde com JSON + `\n` (response)
3. Gateway aguarda resposta com timeout (5 segundos)

**Diferenca do HTTP:**
- Sem headers HTTP (mais leve)
- Ligacao persistente (mais rapido)
- Protocolo customizado (mais flexivel)

**Porque Request-Response sobre TCP proprio e nao HTTP ou AMQP:**

1. **Para que serve este padrao**: O Gateway precisa de enviar dados ao Servidor e receber confirmacao (DATA_ACK). Isto e naturalmente Request-Response — envio um pedido e espero uma resposta

2. **Porque nao usar HTTP para isto**: Ja explicado na Sec 4.2 — HTTP adiciona ~200 bytes de headers por cada medicao. Com TCP puro, o overhead e minimo

3. **Porque nao usar AMQP Request-Response**: O RabbitMQ suporta RPC sobre AMQP (com reply-to queue), mas seria overkill — o Servidor ja esta ligado ao Gateway via TCP, nao precisamos de adicionar mais um broker no meio

4. **Porque e proprio e nao protocolo standard**: Nao existe um protocolo standard simples para "enviar JSON e receber ACK" — HTTP e demasiado pesado, AMQP e complexo, e gRPC e pesado de configurar. O protocolo proprio resolve exatamente o que precisamos com 20 linhas de codigo

**Quando este padrao NAO e adequado:**
- Se precisarmos de streaming (Sensor a enviar dados continuamente) — ai TCP puro sem Request-Response e melhor
- Se precisarmos de multicast — ai Pub/Sub e mais adequado
- Se precisarmos de garantia de entrega com retry automatico — ai AMQP com dead-letter queues e melhor

### 10.3 Healthchecks

Todos os servicos Docker tem healthchecks:
- **Python**: `GET /health` retorna 200 OK
- **.NET**: `grep -q Gateway /proc/1/cmdline`
- **RabbitMQ**: `rabbitmq-diagnostics -q ping`

**Porque sao importantes:**
- O Docker so marca um servico como "healthy" se o healthcheck passar
- Outros servicos podem dependir do estado "healthy"
- Permite monitorizacao automatica

**Porque implementar healthchecks em vez de apenas contar processos:**

1. **Deteccao de deadlocks**: Um processo pode estar "a correr" mas bloqueado (deadlock). O processo nao morre, mas nao responde. Um healthcheck que testa se o servico responde a pedidos deteta esta situacao

2. **Dependencias criticas**: O Gateway pode estar a correr mas o RabbitMQ pode ter caido. Um healthcheck que verifica a ligacao ao RabbitMQ deteta o problema — o Docker pode entao reiniciar o Gateway

3. **Orquestracao**: O Docker Compose com `depends_on` + `condition: service_healthy` garante que o Gateway so arranca depois do RabbitMQ estar realmente funcional (nao apenas "a correr")

4. **Monitorizacao**: `docker-compose ps` mostra o estado de saude de cada servico — facilita a debug em producao

5. **Alertas**: Em producao, healthchecks podem alimentar sistemas de alerta (PagerDuty, Slack) quando um servico fica "unhealthy"

**Porque nao usar Kubernetes probes:**
- Kubernetes tem liveness, readiness, e startup probes — sao mais sofisticados
- Mas para 6 servicos em Docker Compose, healthchecks simples sao suficientes
- Kubernetes probes exigem configuracao adicional que nao justifica a complexidade

### 10.4 Graceful Shutdown

Os servicos Python tratam SIGTERM e SIGINT:
- Param de processar novos pedigos
- Fecham ligacoes existentes
- Libertam recursos
- Exit code 0 (sucesso)

### 10.5 Logs com Rotacao

O `LogHelper` implementa rotacao de logs:
- Tamanho maximo: 5 MB por ficheiro
- Quando o ficheiro atinge o limite, cria um novo
- Previne ocupacao excessiva de disco

**Porque logs com rotacao e nao apenas Console.WriteLine:**

1. **Persistencia**: `Console.WriteLine` vai para o stdout — se o container reiniciar, os logs perdem-se. Logs em ficheiro persistem mesmo apos reinicio

2. **Gestao de disco**: Sem rotacao, um sistema com 6 sensores a logar continuamente enche o disco em dias. A rotacao garante que so guardamos os ultimos N MB de logs

3. **Debug**: Em producao, podemos analisar logs historicos para perceber porque e que o sistema falhou ha 3 horas. Com stdout, so temos os ultimos logs do container

4. **Rotacao e nao retencao ilimitada**: Manter logs infinitos e impossivel — o disco encha. Rotacao de 5MB e um equilibrio entre ter logs suficientes para debug e nao encher o disco

5. **Porque 5MB**: Cada log tem ~100 bytes. 5MB = ~50.000 linhas de log — suficiente para varias horas de operacao com 6 sensores

**Porque nao usar Serilog ou NLog:**
- Serilog e NLog sao frameworks de logging muito completos, mas excessivos para este projeto
- O `LogHelper` customizado tem ~50 linhas — suficiente para rotacao basica
- Menos dependencias = container mais leve = arranque mais rapido

### 10.6 Variaveis de Ambiente

Todos os componentes suportam configuracao via env vars:

| Variavel | Default | Descricao |
|----------|---------|-----------|
| `SERVER_ENDPOINT` | arg[0] | Endpoint do Servidor |
| `CSV_PATH` | dados/gateway.csv | Caminho do CSV de sensores |
| `RABBITMQ_HOST` | rabbitmq | Host do RabbitMQ |
| `RABBITMQ_PORT` | 5672 | Porta do RabbitMQ |
| `LISTEN_PORT` | 7000 | Porta de escuta do Servidor |
| `PRE_PROCESSAMENTO_HOST` | 127.0.0.1 | Host do Pre-Processamento |
| `ANALISE_HOST` | 127.0.0.1 | Host da Analise |
| `ANALISE_RPC_URL` | http://127.0.0.1:6001 | URL base do RPC de Analise |

**Porque variaveis de ambiente e nao ficheiros de configuracao:**

1. **Padrao Docker**: Containers Docker sao projetados para receber configuracao via env vars — e o metodo recomendado pela comunidade Docker

2. **Seguranca**: Variaveis de ambiente nao ficam escritas em ficheiros de configuracao que podem ser commitados acidentalmente no Git. Evita expor secrets (embora neste projeto nao haja auth)

3. **Flexibilidade**: Podemos alterar a configuracao sem recompilar o codigo — basta mudar a variavel de ambiente no `docker-compose.yml`

4. **Overrides**: Podemos sobrescrever variaveis por container — o Gateway pode usar um RabbitMQ host diferente do Sensor, se necessario

5. **Testes**: Em testes, podemos definir variaveis de ambiente diferentes (ex: `RABBITMQ_HOST=localhost`) sem alterar o codigo

**Porque nao usar ficheiros appsettings.json:**
- appsettings.json e o padrao .NET, mas e menos flexivel para Docker
- Variaveis de ambiente sobrescrevem appsettings.json — nao ha conflito
- Para componentes Python, env vars e o padrao (nao usamos appsettings)

**Porque nao usar Consul ou Vault:**
- Consul e Vault sao sistemas de gestao de configuracao para producao — sao excessivos para um projeto academico
- Variaveis de ambiente resolvem o mesmo problema com zero configuracao adicional

---

## GLOSSARIO

| Termo | Definicao |
|-------|-----------|
| **AMQP** | Advanced Message Queuing Protocol — protocolo para mensagens |
| **Backoff** | Atraso progressivo entre tentativas de reconexao |
| **Binding** | Regra que liga uma queue a um exchange |
| **Broker** | intermediario que encaminha mensagens (RabbitMQ) |
| **Consumer** | Quem recebe mensagens (Gateway) |
| **Docker** | Plataforma de virtualizacao com containers |
| **Exchange** | Componente do RabbitMQ que encaminha mensagens |
| **gRPC** | Framework de RPC do Google com Protobuf |
| **Healthcheck** | Verificacao periodica de saude de um servico |
| **JSON** | JavaScript Object Notation — formato de dados textual |
| **Microservicos** | Arquitetura dividida em servicos pequenos e independentes |
| **Mutex** | Mecanismo de sincronizacao para acesso concorrente |
| **Persistencia** | Garantia de que dados nao se perdem |
| **Producer** | Quem envia mensagens (Sensor) |
| **Protobuf** | Protocol Buffers — formato binario do gRPC |
| **Queue** | Fila onde as mensagens ficam armazenadas |
| **Retry** | Tentativa repetida apos falha |
| **Routing Key** | Chave usada para encaminhar mensagens |
| **RPC** | Remote Procedure Call — chamada de funcao remota |
| **SQLite** | Base de dados embutida (embedded) |
| **TCP** | Transmission Control Protocol — transport fiavel |
| **Topic** | Tipo de exchange com routing por padroes |
| **Watchdog** | Thread que monitoriza a saude do sistema |
