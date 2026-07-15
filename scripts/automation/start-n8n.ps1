#requires -Version 7.2

[CmdletBinding()]
param(
    [switch]$NoBuild,
    [ValidateRange(30, 900)] [int]$HealthTimeoutSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$composePath = Join-Path $repositoryRoot "automation/n8n/docker-compose.yml"
if (-not (Get-Command docker -ErrorAction SilentlyContinue))
{
    throw "Docker Desktop and the Docker CLI are required."
}
if (-not (Test-Path -LiteralPath (Join-Path $repositoryRoot ".git") -PathType Container))
{
    throw "The C# repository is unavailable: $repositoryRoot"
}
$null = & docker info --format "{{.ServerVersion}}" 2>$null
if ($LASTEXITCODE -ne 0)
{
    throw "The Docker engine is not running. Start Docker Desktop first."
}
$null = New-Item -ItemType Directory -Path (Join-Path $repositoryRoot "artifacts/upstream") -Force

$composeArguments = @(
    "compose",
    "--project-name", "beyond-aion-automation",
    "--file", $composePath
)
$upArguments = @($composeArguments + "up")
if (-not $NoBuild)
{
    $upArguments += "--build"
}
$upArguments += "--detach"
& docker @upArguments
if ($LASTEXITCODE -ne 0)
{
    throw "Docker Compose failed to start the automation service."
}

$publishedAddress = @(& docker @composeArguments port n8n 5678 2>$null) |
    Select-Object -First 1
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($publishedAddress))
{
    throw "Docker Compose did not report the published n8n port."
}
$portMatch = [regex]::Match($publishedAddress.Trim(), ":(?<port>\d+)$")
if (-not $portMatch.Success)
{
    throw "Could not parse the published n8n address: $publishedAddress"
}
$n8nUrl = "http://localhost:$($portMatch.Groups['port'].Value)"

$deadline = [DateTime]::UtcNow.AddSeconds($HealthTimeoutSeconds)
do
{
    try
    {
        $response = Invoke-WebRequest -UseBasicParsing -Uri "$n8nUrl/healthz" -TimeoutSec 3
        if ($response.StatusCode -eq 200)
        {
            break
        }
    }
    catch
    {
        Start-Sleep -Seconds 2
    }
} while ([DateTime]::UtcNow -lt $deadline)
if ([DateTime]::UtcNow -ge $deadline)
{
    & docker @composeArguments logs --tail 100 n8n
    throw "The n8n container did not become healthy within $HealthTimeoutSeconds seconds."
}

$previousPreference = $ErrorActionPreference
$ErrorActionPreference = "Continue"
$authOutput = @(& docker @composeArguments exec -T n8n codex login status 2>&1 | ForEach-Object { $_.ToString() })
$authExitCode = $LASTEXITCODE
$ErrorActionPreference = $previousPreference

Write-Output "n8n is healthy at $n8nUrl"
Write-Output "Container: beyond-aion-automation"
if ($authExitCode -eq 0)
{
    Write-Output ($authOutput -join [Environment]::NewLine)
}
else
{
    Write-Warning "Codex is not authenticated in the Docker volume. Run scripts/automation/login-codex.ps1."
}
Write-Output "Logs: docker compose -f `"$composePath`" logs -f n8n"
