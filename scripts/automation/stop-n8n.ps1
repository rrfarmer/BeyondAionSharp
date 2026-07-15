#requires -Version 7.2

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$composePath = Join-Path $repositoryRoot "automation/n8n/docker-compose.yml"
& docker compose `
    --project-name beyond-aion-automation `
    --file $composePath `
    down
if ($LASTEXITCODE -ne 0)
{
    throw "Docker Compose failed to stop the automation service."
}
