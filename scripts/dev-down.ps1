$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

param([switch]$RemoveVolumes)

function Get-RepoRoot {
    $scriptDir = $PSScriptRoot
    return (Resolve-Path (Join-Path $scriptDir '..')).Path
}

function Invoke-Compose {
    param([Parameter(Mandatory=$true)][string[]]$Args)

    try {
        & docker compose version *> $null
        & docker compose @Args
        return
    } catch {
        & docker-compose @Args
    }
}

$repoRoot = Get-RepoRoot
Set-Location $repoRoot

$composeFile = Join-Path $repoRoot 'docker-compose.localstack.yml'
$envFile     = Join-Path $repoRoot '.env.localstack'

Write-Host "Stopping LocalStack (InvoiceBilling)..."

$args = @('-f', $composeFile)
if (Test-Path $envFile) { $args += @('--env-file', $envFile) }

$args += 'down'
if ($RemoveVolumes) { $args += '-v' }

Invoke-Compose -Args $args
Write-Host "Done."
