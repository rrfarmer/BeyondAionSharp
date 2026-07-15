#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$UpstreamSha,
    [Parameter(Mandatory)]
    [ValidateSet("ported", "direct-data", "not-applicable", "blocked")]
    [string]$Status,
    [Parameter(Mandatory)] [string]$Notes,
    [string]$CSharpReference = "main",
    [string]$ValidationReport,
    [string]$CSharpRepository,
    [string]$JavaRepository,
    [string]$Remote = "upstream",
    [string]$Branch,
    [switch]$NoFetch,
    [ValidateSet("Json", "Text")] [string]$OutputFormat = "Json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "UpstreamAutomation.psm1") -Force

function ConvertTo-MarkdownCell([string]$Value)
{
    return (($Value -replace "\r?\n", " ") -replace "\|", "\|").Trim()
}

$csharpRoot = Get-CSharpRepositoryRoot -Path $CSharpRepository
$javaRoot = Get-JavaRepositoryRoot -CSharpRepository $csharpRoot -Path $JavaRepository
$worktree = Assert-CSharpMainWorktree -CSharpRepository $csharpRoot
$scan = Get-UpstreamScan `
    -CSharpRepository $csharpRoot `
    -JavaRepository $javaRoot `
    -Remote $Remote `
    -Branch $Branch `
    -NoFetch:$NoFetch `
    -SkipPullRequests
if ($scan.pendingCount -eq 0)
{
    throw "No merged Java commit is pending completion."
}

$commit = $scan.nextPending
if (-not [string]::IsNullOrWhiteSpace($UpstreamSha) -and $UpstreamSha -ne $commit.sha)
{
    throw "Only the first pending Java commit can be completed. Expected $($commit.sha), received $UpstreamSha."
}
$UpstreamSha = $commit.sha
if ([string]::IsNullOrWhiteSpace($Notes))
{
    throw "Notes must describe the port, validation boundary, or reason for the decision."
}
if ($Status -in @("not-applicable", "blocked") -and -not $worktree.clean)
{
    throw "Status '$Status' requires a clean worktree before the tracker files are updated. Remove partial product changes first."
}
if ($Status -in @("ported", "direct-data") -and $worktree.clean)
{
    throw "Status '$Status' requires reviewed implementation or data changes in the C# worktree."
}

$packagePath = Get-ChildItem -LiteralPath (Join-Path $csharpRoot "artifacts/upstream") -Directory -ErrorAction SilentlyContinue |
    Where-Object {
        $metadataPath = Join-Path $_.FullName "metadata.json"
        if (-not (Test-Path -LiteralPath $metadataPath -PathType Leaf)) { return $false }
        try { (Get-Content -LiteralPath $metadataPath -Raw | ConvertFrom-Json).upstreamSha -eq $UpstreamSha }
        catch { $false }
    } |
    Select-Object -First 1 -ExpandProperty FullName
if ([string]::IsNullOrWhiteSpace($packagePath))
{
    throw "No prepared package exists for $UpstreamSha. Run scripts/upstream/prepare-next.ps1 first."
}

if ($Status -in @("ported", "direct-data"))
{
    if ([string]::IsNullOrWhiteSpace($ValidationReport))
    {
        $ValidationReport = Join-Path $packagePath "validation.json"
    }
    elseif (-not [System.IO.Path]::IsPathRooted($ValidationReport))
    {
        $ValidationReport = Join-Path $csharpRoot $ValidationReport
    }
    if (-not (Test-Path -LiteralPath $ValidationReport -PathType Leaf))
    {
        throw "A passing validation report is required for status '$Status': $ValidationReport"
    }
    $validation = Get-Content -LiteralPath $ValidationReport -Raw | ConvertFrom-Json
    if ($validation.status -ne "passed" -or $validation.upstreamSha -ne $UpstreamSha)
    {
        throw "Validation report does not record a passing result for $UpstreamSha."
    }
    $currentHead = (Invoke-GitCommand -Repository $csharpRoot -Arguments @("rev-parse", "HEAD")).Text.Trim()
    if ($validation.csharpHead -ne $currentHead)
    {
        throw "The C# HEAD changed after validation. Run validate-port.ps1 again."
    }
    $currentFingerprint = Get-CSharpWorktreeFingerprint -CSharpRepository $csharpRoot
    if ($validation.worktreeFingerprint -ne $currentFingerprint.sha256)
    {
        throw "The C# worktree changed after validation. Run validate-port.ps1 again."
    }
}

$ledgerStatus = switch ($Status)
{
    "ported" { "Ported" }
    "direct-data" { "Direct data carryover" }
    "not-applicable" { "Not applicable" }
    "blocked" { "Blocked" }
}
$logPath = Join-Path $csharpRoot "docs/upstream-port-log.md"
$logLines = [System.Collections.Generic.List[string]]::new()
foreach ($line in Get-Content -LiteralPath $logPath)
{
    $logLines.Add($line)
}
$shortSha = $UpstreamSha.Substring(0, 9)
$backtick = [char]96
$row = "| $backtick$shortSha$backtick | $($commit.date) | $(ConvertTo-MarkdownCell $commit.subject) | $ledgerStatus | $(ConvertTo-MarkdownCell $CSharpReference) | $(ConvertTo-MarkdownCell $Notes) |"
$matchingIndexes = @()
for ($index = 0; $index -lt $logLines.Count; $index++)
{
    if ($logLines[$index] -match "^\|\s*\x60?$([regex]::Escape($shortSha))\x60?\s*\|")
    {
        $matchingIndexes += $index
    }
}
if ($matchingIndexes.Count -gt 1)
{
    throw "The upstream ledger contains duplicate rows for $shortSha."
}
if ($matchingIndexes.Count -eq 1)
{
    $logLines[$matchingIndexes[0]] = $row
}
else
{
    $logLines.Add($row)
}
$null = Write-Utf8File -Path $logPath -Content (($logLines -join [Environment]::NewLine) + [Environment]::NewLine)

$statePath = Join-Path $csharpRoot "docs/upstream-port-state.json"
$state = Get-UpstreamPortState -CSharpRepository $csharpRoot
if ($Status -ne "blocked")
{
    $state.lastCompletedJavaCommit = $UpstreamSha
}
if ($state.PSObject.Properties["lastScannedJavaCommit"])
{
    $state.lastScannedJavaCommit = $scan.remoteHead
}
else
{
    $state | Add-Member -NotePropertyName lastScannedJavaCommit -NotePropertyValue $scan.remoteHead
}
$state.updatedUtc = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
$null = Write-AutomationJson -InputObject $state -Path $statePath

$subjectPrefix = switch ($Status)
{
    "ported" { "Port" }
    "direct-data" { "Carry over" }
    "not-applicable" { "Record non-applicable" }
    "blocked" { "Record blocked" }
}
$commitMessage = @"
$subjectPrefix $($commit.subject)

Upstream-Java-SHA: $UpstreamSha
Port-Status: $Status
"@
$commitMessagePath = Write-Utf8File -Path (Join-Path $packagePath "commit-message.txt") -Content $commitMessage
$result = [pscustomobject][ordered]@{
    schemaVersion = 1
    status = "recorded"
    portStatus = $Status
    upstreamSha = $UpstreamSha
    advanced = $Status -ne "blocked"
    lastCompletedJavaCommit = [string]$state.lastCompletedJavaCommit
    ledgerPath = $logPath
    statePath = $statePath
    commitMessagePath = $commitMessagePath
}

if ($OutputFormat -eq "Text")
{
    Write-Output "Recorded $UpstreamSha as '$ledgerStatus'."
    Write-Output "State advanced: $($result.advanced)"
    Write-Output "Commit message: $commitMessagePath"
    Write-Output "Review and stage the intended implementation plus both tracker files, then run:"
    Write-Output "git commit -F `"$commitMessagePath`""
}
else
{
    Write-Output (ConvertTo-AutomationJson -InputObject $result -Compress)
}
