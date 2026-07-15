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
    exec n8n codex login --device-auth
if ($LASTEXITCODE -ne 0)
{
    throw "Codex device authentication failed."
}
& docker compose `
    --project-name beyond-aion-automation `
    --file $composePath `
    exec -T n8n codex login status
exit $LASTEXITCODE
