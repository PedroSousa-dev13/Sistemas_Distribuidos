# ?? Teste Rápido - Sistema IoT Distribuído - FASE 3

## ? Compilação
O projeto foi compilado com sucesso em .NET 9!

---

## ?? Instruções de Teste com 3 Terminais

### **TERMINAL 1 - SERVIDOR** ???

```powershell
cd C:\Users\utilizador\source\repos\Sistemas_Distribuidos
dotnet run --project src/Servidor/Servidor.csproj 7000
```

**Esperado:**
- Banner do Servidor
- Mensagem: "? SERVIDOR INICIADO COM SUCESSO!"
- "?? Escutando para conexões na porta 7000..."

---

### **TERMINAL 2 - GATEWAY** ??

```powershell
cd C:\Users\utilizador\source\repos\Sistemas_Distribuidos
dotnet run --project src/Gateway/Gateway.csproj 5000 127.0.0.1:7000 sensores.csv
```

**Esperado:**
- Banner da Gateway
- "? CSV carregado com sucesso: 3 sensor(es) encontrado(s)"
- "? Conectado ao servidor com SUCESSO!"
- "? GATEWAY INICIADA COM SUCESSO!"
- "?? Aguardando conexões de sensores na porta 5000..."

---

### **TERMINAL 3 - SENSOR** ??

```powershell
cd C:\Users\utilizador\source\repos\Sistemas_Distribuidos
dotnet run --project src/Sensor/Sensor.csproj 127.0.0.1 5000 SENSOR_TEMP_01
```

**Esperado:**
- Banner do Sensor
- "? Conectado com sucesso!"
- Menu interativo com opções [1-8] para enviar dados
- Seleciona [1] para enviar temperatura

---

## ?? Fluxo de Mensagens Esperadas

1. **Sensor** ? Envia REGISTER
2. **Gateway** ? Processa e encaminha para Servidor
3. **Servidor** ? Persiste dados
4. **Confirmações** ?? Viajam em ambas as direções

---

## ?? Mensagens de Sucesso Detalhadas

### No SERVIDOR:
```
? [DADOS PERSISTIDOS] Sensor: SENSOR_TEMP_01 | Tipo: temperatura | Valor: 23.5 | Hora: 2024-01-15T...
?? Gateway #1 conectada! (Total: 1)
?? Gateway desconectada | Mensagens processadas: 5 | Total de gateways: 0
```

### Na GATEWAY:
```
? [REGISTO] Sensor 'SENSOR_TEMP_01' registado com SUCESSO! (Zona: Zona A)
?? [DADOS] Sensor 'SENSOR_TEMP_01' ? temperatura: 23.5 (enfileirada para servidor)
?? [HEARTBEAT] Sensor 'SENSOR_TEMP_01' está vivo (Zona: Zona A)
? Mensagem DATA de SENSOR_TEMP_01 encaminhada com sucesso. ACK recebida do servidor.
```

### No SENSOR:
```
[REGISTO OK] Registado na gateway com sucesso!
? Medição de temperatura enviada com sucesso.
?? Heartbeat enviado ao vivo
```

---

## ?? Troubleshooting

Se a porta 7000 já está em uso:
```powershell
netstat -ano | findstr :7000
taskkill /PID <PID> /F
```

Ou muda para outra porta (ex: 8000):
```powershell
# Terminal 1
dotnet run --project src/Servidor/Servidor.csproj 8000

# Terminal 2 (ajusta endpoint)
dotnet run --project src/Gateway/Gateway.csproj 5000 127.0.0.1:8000 sensores.csv
```

---

## ? Ficheiros de Saída

- `dados/servidor.log` - Log detalhado do servidor
- `gateway.log` - Log detalhado da gateway
- `dados/temperatura.txt` - Dados persistidos de temperatura
- `dados/humidade.txt` - Dados persistidos de humidade
- etc.

Boa sorte! ??
