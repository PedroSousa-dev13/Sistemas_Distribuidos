# ?? Quick Start - Fase 3 Servidor

## 1?? Compilação

```bash
# Compilar tudo
dotnet build

# Ou específico do servidor
cd src/Servidor
dotnet build
```

## 2?? Execução

### Terminal 1: Servidor
```bash
cd src/Servidor
dotnet run 6000
```

Saída esperada:
```
Servidor escutando no porto 6000
[log] 2024-01-15T10:30:00.000Z: Conectado ao servidor.
[log] 2024-01-15T10:30:05.000Z: Nova gateway conectada. Total: 1
[log] 2024-01-15T10:30:10.000Z: Medição de temperatura de SENSOR_001 persistida.
```

### Terminal 2: Gateway
```bash
cd src/Gateway
dotnet run 5000 localhost:6000 sensores.csv
```

### Terminal 3: Sensor(es)
```bash
cd src/Sensor
dotnet run localhost 5000 SENSOR_001
```

## 3?? Verificar Dados Persistidos

```bash
# Ver ficheiro de temperatura
cat src/Servidor/dados/temperatura.txt

# Ver log do servidor
cat src/Servidor/dados/servidor.log

# Contar linhas persistidas
wc -l src/Servidor/dados/*.txt
```

## 4?? Testar

### Testes Unitários
```bash
cd tests/Servidor.Tests
dotnet test
```

### Teste de Concorrência (múltiplos sensores)
```bash
# Terminal 3a
dotnet run localhost 5000 SENSOR_001

# Terminal 3b (novo)
dotnet run localhost 5000 SENSOR_002

# Terminal 3c (novo)
dotnet run localhost 5000 SENSOR_003
```

Verifique `servidor.log` para confirmar que as 3 foram registadas.

## 5?? Estrutura de Dados

### Ficheiros Criados em `src/Servidor/dados/`

```
??? temperatura.txt           # Medições de temperatura
??? humidade.txt              # Medições de humidade
??? qualidade_ar.txt          # Índice de qualidade do ar
??? ruido.txt                 # Níveis de ruído
??? pm25.txt                  # Partículas PM2.5
??? pm10.txt                  # Partículas PM10
??? luminosidade.txt          # Níveis de luminosidade
??? imagem.txt                # Dados de imagem/vídeo
??? servidor.log              # Log de atividades
```

### Formato de Linha
```
2024-01-15T10:30:00.000Z|SENSOR_001|23.5
2024-01-15T10:30:05.000Z|SENSOR_002|24.2
2024-01-15T10:30:10.000Z|SENSOR_001|23.7
```

## 6?? Argumentos de Linha de Comandos

### Servidor
```bash
dotnet run <portoEscuta>
```
- **portoEscuta:** Porto TCP onde o servidor escuta (ex: 6000)

### Gateway
```bash
dotnet run <portoEscuta> <servidorEndpoint> <caminhoCSV>
```
- **portoEscuta:** Porto para sensores (ex: 5000)
- **servidorEndpoint:** IP:Porto do servidor (ex: localhost:6000)
- **caminhoCSV:** Caminho do ficheiro de sensores (ex: sensores.csv)

### Sensor
```bash
dotnet run <ipGateway> <portoGateway> <sensorId>
```
- **ipGateway:** IP da gateway (ex: localhost)
- **portoGateway:** Porto da gateway (ex: 5000)
- **sensorId:** ID único do sensor (ex: SENSOR_001)

## 7?? Troubleshooting

### Erro: "Address already in use"
**Problema:** Porto já está em uso
**Solução:** Use outro porto ou mude o processo que usa o porto

```bash
# Encontrar processo usando porto 6000 (Linux/Mac)
lsof -i :6000

# Encontrar processo usando porto 6000 (Windows)
netstat -ano | findstr :6000
```

### Erro: "Connection refused"
**Problema:** Servidor não está a correr
**Solução:** Verifique se o servidor foi iniciado no terminal 1

### Dados não aparecem
**Problema:** Ficheiros vazios em `dados/`
**Solução:** 
1. Verifique se o sensor está a enviar dados (menu)
2. Verifique se a gateway conectou ao servidor (log)
3. Verifique `servidor.log` para erros

### Log muito grande
**Problema:** `servidor.log` ficar muito grande
**Solução:** Delete o ficheiro (servidor recria automaticamente)

```bash
rm src/Servidor/dados/servidor.log
```

## 8?? Diagrama de Fluxo

```
??????????????????????????????????????????????????
?         TESTE COMPLETO (3 Terminais)           ?
??????????????????????????????????????????????????
?                                                ?
?  Terminal 1:                                  ?
?  $ dotnet run 6000                           ?
?  Servidor escutando no porto 6000             ?
?                                                ?
?  Terminal 2:                                  ?
?  $ dotnet run 5000 localhost:6000 sensores.csv?
?  Gateway escutando no porto 5000              ?
?  Conectando ao servidor...                    ?
?                                                ?
?  Terminal 3:                                  ?
?  $ dotnet run localhost 5000 SENSOR_001       ?
?  Menu do Sensor ? escolher opção 1 (Temp)    ?
?  Inserir valor: 23.5                          ?
?                                                ?
?  Resultado:                                   ?
?  Terminal 1: [LOG] Medição persistida         ?
?  Ficheiro: src/Servidor/dados/temperatura.txt?
?  Conteúdo: 2024-01-15T...|SENSOR_001|23.5   ?
?                                                ?
??????????????????????????????????????????????????
```

## ? Checklist de Teste

- [ ] Servidor inicia no porto 6000
- [ ] Gateway conecta ao servidor
- [ ] Sensor conecta à gateway
- [ ] Sensor consegue enviar medição
- [ ] Ficheiro de temperatura é criado
- [ ] Dados aparecem no ficheiro (formato correto)
- [ ] Servidor.log mostra atividade
- [ ] Múltiplos sensores funcionam em simultâneo
- [ ] Dados são persistidos sem erro
- [ ] Testes unitários passam (8/8)

---

**Tudo pronto! Próximo passo: testa e diverte-te com dados em tempo real! ??**
