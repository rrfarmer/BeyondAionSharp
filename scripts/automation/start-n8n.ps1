#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$CSharpRepository,
    [string]$JavaRepository,
    [ValidateRange(1, 65535)] [int]$Port = 5678,
    [string]$N8nVersion = "2.26.8",
    [string]$UserFolder,
    [string]$TimeZone = "America/New_York"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "..\upstream\UpstreamAutomation.psm1") -Force

$csharpRoot = Get-CSharpRepositoryRoot -Path $CSharpRepository
$javaRoot = Get-JavaRepositoryRoot -CSharpRepository $csharpRoot -Path $JavaRepository
$nodeCommand = Get-Command node -ErrorAction SilentlyContinue
$npxCommand = Get-Command npx -ErrorAction SilentlyContinue
if (-not $nodeCommand -or -not $npxCommand)
{
    throw "Node.js and npx are required. Install a supported Node.js release before starting n8n."
}

$rawNodeVersion = (& $nodeCommand.Source --version).Trim().TrimStart("v")
$nodeVersion = [version]$rawNodeVersion
if ($nodeVersion -lt [version]"20.19.0" -or $nodeVersion.Major -gt 24)
{
    throw "n8n $N8nVersion requires Node.js 20.19 through 24.x; active version is $rawNodeVersion."
}

$occupied = [System.Net.NetworkInformation.IPGlobalProperties]::GetIPGlobalProperties().GetActiveTcpListeners() |
    Where-Object { $_.Port -eq $Port } |
    Select-Object -First 1
if ($occupied)
{
    throw "TCP port $Port is already in use at $($occupied.Address)."
}

if ([string]::IsNullOrWhiteSpace($UserFolder))
{
    $UserFolder = Join-Path $HOME ".n8n-beyond-aion-sharp"
}
$UserFolder = [System.IO.Path]::GetFullPath($UserFolder)
$null = New-Item -ItemType Directory -Path $UserFolder -Force

$env:BEYOND_AION_CSHARP_ROOT = $csharpRoot
$env:BEYOND_AION_JAVA_ROOT = $javaRoot
$env:N8N_USER_FOLDER = $UserFolder
$env:N8N_HOST = "localhost"
$env:N8N_LISTEN_ADDRESS = "127.0.0.1"
$env:N8N_PORT = $Port.ToString()
$env:N8N_PROTOCOL = "http"
$env:GENERIC_TIMEZONE = $TimeZone
$env:N8N_DIAGNOSTICS_ENABLED = "false"
$env:N8N_PERSONALIZATION_ENABLED = "false"
$env:N8N_TEMPLATES_ENABLED = "false"
# Execute Command is required by the imported workflow. Keep Local File Trigger disabled.
$env:NODES_EXCLUDE = '["n8n-nodes-base.localFileTrigger"]'

Write-Host "Starting n8n $N8nVersion at http://localhost:$Port"
Write-Host "n8n data: $UserFolder"
Write-Host "C# repository: $csharpRoot"
Write-Host "Java repository: $javaRoot"
Write-Host "Stop n8n with Ctrl+C."

& $npxCommand.Source --yes "n8n@$N8nVersion" start
exit $LASTEXITCODE
