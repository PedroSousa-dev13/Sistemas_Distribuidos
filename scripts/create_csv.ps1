$csvPath = "sensores.csv"
$csvPath = "sensores.csv"
$lines = @(
    "SENSOR_01|ativo|Sala_A|[temperatura,humidade]|2024-01-15T10:30:00.0000000Z",
    "SENSOR_02|ativo|Sala_B|[pressao,temperatura]|2024-01-15T10:32:15.0000000Z",
    "SENSOR_03|ativo|Corredor|[luz,movimento]|2024-01-15T10:28:45.0000000Z"
)

Set-Content -Path $csvPath -Value $lines -Encoding UTF8
Write-Host "Ficheiro sensores.csv criado com sucesso!"
Get-Content $csvPath
