#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$CSharpRepository,
    [string]$JavaRepository,
    [string]$Remote = "upstream",
    [string]$Branch,
    [switch]$NoFetch
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "UpstreamAutomation.psm1") -Force
$csharpRoot = Get-CSharpRepositoryRoot -Path $CSharpRepository
$javaRoot = Get-JavaRepositoryRoot -CSharpRepository $csharpRoot -Path $JavaRepository
$scan = Get-UpstreamScan `
    -CSharpRepository $csharpRoot `
    -JavaRepository $javaRoot `
    -Remote $Remote `
    -Branch $Branch `
    -NoFetch:$NoFetch `
    -SkipPullRequests
foreach ($commit in $scan.pending)
{
    Write-Output "$($commit.sha)`t$($commit.date)`t$($commit.subject)"
}
