#Requires -Version 5.1
<#
.SYNOPSIS
  Gera todos os segredos locais em ./secrets (ignorado pelo git). Equivalente ao generate-secrets.sh.
  Funciona no Windows PowerShell 5.1 (nativo do Windows) e no PowerShell 7+.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\generate-secrets.ps1
  pwsh ./scripts/generate-secrets.ps1 -Force -DbHost localhost -MqHost localhost
#>
param(
    [switch]$Force,
    [string]$DbHost = 'postgres',
    [string]$MqHost = 'rabbitmq'
)
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$out = Join-Path $root 'secrets'

if ((Test-Path $out) -and -not $Force) {
    Write-Host 'secrets/ já existe. Use -Force para regenerar (invalida dados/tokens existentes).'
    exit 0
}

$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
function New-RandomBytes([int]$count) {
    $bytes = New-Object byte[] $count
    $rng.GetBytes($bytes)
    $bytes
}
function New-Password([int]$length = 32) {
    $chars = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
    -join (New-RandomBytes $length | ForEach-Object { $chars[$_ % $chars.Length] })
}
function New-Key32 { [Convert]::ToBase64String((New-RandomBytes 32)) }
function Write-Secret([string]$path, [string]$value) {
    [System.IO.File]::WriteAllText($path, $value, (New-Object System.Text.UTF8Encoding($false)))
}
# Chave RSA 3072 em PEM PKCS#8. O PowerShell 7 (.NET 7+) exporta direto; no Windows PowerShell 5.1
# (.NET Framework) a exportação PKCS#8 é feita pela CNG do Windows.
function New-RsaPkcs8Pem {
    $rsa = [System.Security.Cryptography.RSA]::Create()
    if ($rsa.PSObject.Methods.Name -contains 'ExportPkcs8PrivateKeyPem') {
        $rsa = [System.Security.Cryptography.RSA]::Create(3072)
        return $rsa.ExportPkcs8PrivateKeyPem()
    }
    $params = New-Object System.Security.Cryptography.CngKeyCreationParameters
    $params.ExportPolicy = [System.Security.Cryptography.CngExportPolicies]::AllowPlaintextExport
    $params.Parameters.Add((New-Object System.Security.Cryptography.CngProperty('Length', [BitConverter]::GetBytes(3072), [System.Security.Cryptography.CngPropertyOptions]::None)))
    $key = [System.Security.Cryptography.CngKey]::Create([System.Security.Cryptography.CngAlgorithm]::Rsa, $null, $params)
    try {
        $der = $key.Export([System.Security.Cryptography.CngKeyBlobFormat]::Pkcs8PrivateBlob)
    } finally {
        $key.Dispose()
    }
    $b64 = [Convert]::ToBase64String($der, [System.Base64FormattingOptions]::InsertLineBreaks) -replace "`r`n", "`n"
    "-----BEGIN PRIVATE KEY-----`n$b64`n-----END PRIVATE KEY-----"
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
'infra', 'transactions', 'consolidation', 'identity' | ForEach-Object { New-Item -ItemType Directory -Path (Join-Path $out $_) | Out-Null }

$pgAdmin = New-Password; $pgTx = New-Password; $pgCo = New-Password; $pgId = New-Password
$mqPass = New-Password
$operatorPass = "Op-$(New-Password 20)"
$analystPass = "An-$(New-Password 20)"

Write-Secret "$out/infra/postgres_admin_password" $pgAdmin
Write-Secret "$out/infra/pg_transactions_password" $pgTx
Write-Secret "$out/infra/pg_consolidation_password" $pgCo
Write-Secret "$out/infra/pg_identity_password" $pgId
Write-Secret "$out/infra/rabbitmq.conf" "default_user = cashflow`ndefault_pass = $mqPass`nloopback_users = none`n"

Write-Secret "$out/transactions/ConnectionStrings__Transactions" "Host=$DbHost;Port=5432;Database=cashflow_transactions;Username=svc_transactions;Password=$pgTx;Maximum Pool Size=60"
Write-Secret "$out/transactions/RabbitMq__Password" $mqPass
Write-Secret "$out/transactions/RabbitMq__HostName" $MqHost
Write-Secret "$out/transactions/Pii__EncryptionKeys__k1" (New-Key32)
Write-Secret "$out/transactions/Pii__BlindIndexKey" (New-Key32)

Write-Secret "$out/consolidation/ConnectionStrings__Consolidation" "Host=$DbHost;Port=5432;Database=cashflow_consolidation;Username=svc_consolidation;Password=$pgCo;Maximum Pool Size=60"
Write-Secret "$out/consolidation/RabbitMq__Password" $mqPass
Write-Secret "$out/consolidation/RabbitMq__HostName" $MqHost

Write-Secret "$out/identity/ConnectionStrings__Identity" "Host=$DbHost;Port=5432;Database=cashflow_identity;Username=svc_identity;Password=$pgId"
Write-Secret "$out/identity/Jwt__SigningKeyPem" (New-RsaPkcs8Pem)
Write-Secret "$out/identity/Identity__SeedUsers__0__Password" $operatorPass
Write-Secret "$out/identity/Identity__SeedUsers__1__Password" $analystPass

Write-Host "Segredos gerados em $out"
Write-Host "Usuário 'operador' (escrita) - senha em secrets/identity/Identity__SeedUsers__0__Password"
Write-Host "Usuário 'analista' (leitura) - senha em secrets/identity/Identity__SeedUsers__1__Password"
