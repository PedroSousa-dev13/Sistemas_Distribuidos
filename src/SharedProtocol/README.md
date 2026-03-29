# SharedProtocol

Biblioteca de classes C# que implementa o protocolo de comunicação partilhado para o sistema de monitorização ambiental urbana.

## Estrutura

- **src/SharedProtocol/** - Biblioteca de classes principal
- **tests/SharedProtocol.Tests/** - Projeto de testes xUnit com FsCheck

## Dependências

- .NET 8.0
- System.Text.Json 8.0.5
- xUnit (testes)
- FsCheck 2.16.6 (property-based testing)
- FsCheck.Xunit 2.16.6

## Comandos

```bash
# Restaurar pacotes
dotnet restore

# Compilar
dotnet build

# Executar testes
dotnet test
```
