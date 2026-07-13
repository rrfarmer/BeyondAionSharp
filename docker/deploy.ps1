# One-command first-time deploy for the Aion server stack (Windows PowerShell).
#   Right-click > Run with PowerShell, or:  powershell -ExecutionPolicy Bypass -File docker\deploy.ps1
$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

if (-not (Test-Path ".env")) {
    Copy-Item ".env.example" ".env"
    Write-Host "Created docker\.env from the template."
    Write-Host ">> Open docker\.env and set SERVER_HOST to the address players will use,"
    Write-Host ">> then run this script again.  (Default 127.0.0.1 works for this PC only.)"
}

Write-Host "Building images (the first build can take several minutes)..."
docker compose --env-file .env build
if ($LASTEXITCODE -ne 0) { throw "docker compose build failed" }

Write-Host "Starting the stack..."
docker compose --env-file .env up -d
if ($LASTEXITCODE -ne 0) { throw "docker compose up failed" }

Write-Host ""
Write-Host "Aion server stack is starting up."
Write-Host "  Status:  docker compose -f docker\docker-compose.yml ps"
Write-Host "  Logs:    docker compose -f docker\docker-compose.yml logs -f"
Write-Host "  Stop:    docker\stop.ps1"
