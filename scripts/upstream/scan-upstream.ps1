#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$CSharpRepository,
    [string]$JavaRepository,
    [string]$Remote = "upstream",
    [string]$Branch,
    [switch]$NoFetch,
    [switch]$SkipPullRequests,
    [string]$OutputPath,
    [ValidateSet("Json", "Text")] [string]$OutputFormat = "Json"
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
    -SkipPullRequests:$SkipPullRequests

if ([string]::IsNullOrWhiteSpace($OutputPath))
{
    $OutputPath = Join-Path $csharpRoot "artifacts\upstream\latest-scan.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath))
{
    $OutputPath = Join-Path $csharpRoot $OutputPath
}
$writtenPath = Write-AutomationJson -InputObject $scan -Path $OutputPath

if ($OutputFormat -eq "Text")
{
    Write-Output "Java upstream: $($scan.remoteRef) at $($scan.remoteHead)"
    Write-Output "Last completed: $($scan.lastCompletedJavaCommit)"
    Write-Output "Pending merged commits: $($scan.pendingCount)"
    foreach ($commit in $scan.pending)
    {
        Write-Output "$($commit.sha)`t$($commit.date)`t$($commit.subject)"
    }
    Write-Output "Open PRs targeting $($scan.branch): $($scan.openPullRequestCount)"
    foreach ($pullRequest in $scan.openPullRequests)
    {
        Write-Output "#$($pullRequest.number)`t$($pullRequest.headSha)`t$($pullRequest.title)`t$($pullRequest.url)"
    }
    foreach ($warning in $scan.warnings)
    {
        Write-Output "WARNING: $warning"
    }
    Write-Output "Snapshot: $writtenPath"
}
else
{
    Write-Output (ConvertTo-AutomationJson -InputObject $scan -Compress)
}
