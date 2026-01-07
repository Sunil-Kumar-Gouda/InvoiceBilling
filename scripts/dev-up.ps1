$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

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

function Read-EnvFile([string]$path) {
    $map = @{}
    if (-not (Test-Path $path)) { return $map }

    Get-Content $path |
        Where-Object { $_ -and -not $_.TrimStart().StartsWith('#') } |
        ForEach-Object {
            $line = $_.Trim()
            if ($line -match '^(?<k>[^=]+)=(?<v>.*)$') {
                $map[$Matches['k'].Trim()] = $Matches['v'].Trim()
            }
        }
    return $map
}

$repoRoot = Get-RepoRoot
Set-Location $repoRoot

$composeFile = Join-Path $repoRoot 'docker-compose.localstack.yml'
$envFile     = Join-Path $repoRoot '.env.localstack'

Write-Host "Starting LocalStack (InvoiceBilling)..."

if (Test-Path $envFile) {
    Invoke-Compose -Args @('-f', $composeFile, '--env-file', $envFile, 'up', '-d')
} else {
    Invoke-Compose -Args @('-f', $composeFile, 'up', '-d')
}

$healthUrl = 'http://localhost:4566/_localstack/health'
Write-Host "Waiting for LocalStack health endpoint..."

$ready = $false
for ($i = 1; $i -le 60; $i++) {
    try {
        Invoke-RestMethod -Uri $healthUrl -Method Get -TimeoutSec 2 | Out-Null
        $ready = $true
        break
    } catch {
        Start-Sleep -Seconds 1
    }
}

if (-not $ready) {
    Write-Warning "LocalStack did not become ready at $healthUrl. Check: docker logs invoicebilling-localstack"
    exit 1
}

$env = Read-EnvFile $envFile
$bucket = $env['INVOICEBILLING_S3_BUCKET']; if (-not $bucket) { $bucket = 'invoicebilling-invoices' }
$queue  = $env['INVOICEBILLING_SQS_QUEUE']; if (-not $queue)  { $queue  = 'invoicebilling-jobs' }

Write-Host "LocalStack is ready."
Write-Host "  Edge URL : http://localhost:4566"
Write-Host "  S3 Bucket: $bucket"
Write-Host "  SQS Queue: $queue"

Write-Host "Verifying resources inside container..."
try {
    & docker exec invoicebilling-localstack awslocal s3 ls | Out-Host
    & docker exec invoicebilling-localstack awslocal sqs list-queues | Out-Host
} catch {
    Write-Warning "Could not verify resources via awslocal. Init script will create them when LocalStack is ready."
}

Write-Host "Done. Next: run the API with ASPNETCORE_ENVIRONMENT=Development."
