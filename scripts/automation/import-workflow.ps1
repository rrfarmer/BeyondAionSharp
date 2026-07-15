#requires -Version 7.2

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$composePath = Join-Path $repositoryRoot "automation/n8n/docker-compose.yml"
$workflowPath = "/workspace/csharp/automation/n8n/workflows/upstream-monitor.json"
$workflowId = "HmUxQf6s9T2kL4vN"

& docker compose `
    --project-name beyond-aion-automation `
    --file $composePath `
    exec -T n8n n8n import:workflow --input=$workflowPath
if ($LASTEXITCODE -ne 0)
{
    throw "The n8n workflow import failed. Start the container first."
}

$workflows = @(& docker compose `
    --project-name beyond-aion-automation `
    --file $composePath `
    exec -T n8n n8n list:workflow)
if ($LASTEXITCODE -ne 0 -or -not ($workflows -match "^$workflowId\|"))
{
    throw "The imported workflow was not found in n8n."
}

Write-Output "Imported inactive workflow: BeyondAionSharp - Port Java commits with Codex"
Write-Output "Open the URL reported by start-n8n.ps1, run the workflow manually once, then publish it to enable the schedule."
