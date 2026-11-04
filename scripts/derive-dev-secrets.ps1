#Requires -Version 5.1
<#
.SYNOPSIS
  Deriva secrets/dev a partir de secrets/ trocando os hosts do Docker (postgres, rabbitmq) por localhost.
  Usado pelos perfis de launchSettings para debugar as APIs no Visual Studio com a infra no Docker.
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\scripts\derive-dev-secrets.ps1
#>
$ErrorActionPreference = 'Stop'

$root = Resolve-Path (Join-Path $PSScriptRoot '..')
$src = Join-Path $root 'secrets'
$out = Join-Path $src 'dev'

if (-not (Test-Path (Join-Path $src 'identity'))) {
    Write-Host 'secrets/ não encontrado. Rode antes scripts/generate-secrets.ps1.'
    exit 1
}

if (Test-Path $out) { Remove-Item $out -Recurse -Force }
foreach ($svc in 'identity', 'transactions', 'consolidation') {
    New-Item -ItemType Directory -Path (Join-Path $out $svc) | Out-Null
    Get-ChildItem (Join-Path $src $svc) -File | ForEach-Object {
        $value = [System.IO.File]::ReadAllText($_.FullName)
        if ($_.Name -like 'ConnectionStrings__*') { $value = $value -replace 'Host=[^;]+', 'Host=localhost' }
        if ($_.Name -eq 'RabbitMq__HostName') { $value = 'localhost' }
        [System.IO.File]::WriteAllText((Join-Path (Join-Path $out $svc) $_.Name), $value, (New-Object System.Text.UTF8Encoding($false)))
    }
}
Write-Host "secrets/dev gerado em $out"
