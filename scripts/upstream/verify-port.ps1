#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$UpstreamSha,
    [string]$CSharpRepository,
    [string]$JavaRepository,
    [ValidateSet("Json", "Text")] [string]$OutputFormat = "Text"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "UpstreamAutomation.psm1") -Force

function Get-ExactTrailer([string]$Message, [string]$Name)
{
    $values = @(
        $Message -split "\r?\n" |
            Where-Object { $_ -match "^$([regex]::Escape($Name)):\s*(?<value>.+?)\s*$" } |
            ForEach-Object { if ($_ -match ":\s*(?<value>.+?)\s*$") { $Matches.value } }
    )
    if ($values.Count -ne 1)
    {
        throw "Expected exactly one '$Name' trailer; found $($values.Count)."
    }
    return $values[0]
}

$csharpRoot = Get-CSharpRepositoryRoot -Path $CSharpRepository
$javaRoot = Get-JavaRepositoryRoot -CSharpRepository $csharpRoot -Path $JavaRepository
$null = Assert-CSharpMainWorktree -CSharpRepository $csharpRoot -RequireClean
$state = Get-UpstreamPortState -CSharpRepository $csharpRoot
$head = (Invoke-GitCommand -Repository $csharpRoot -Arguments @("rev-parse", "HEAD")).Text.Trim()
$message = (Invoke-GitCommand -Repository $csharpRoot -Arguments @("show", "-s", "--format=%B", $head)).Text
$headUpstreamSha = Get-ExactTrailer -Message $message -Name "Upstream-Java-SHA"
$headPortStatus = Get-ExactTrailer -Message $message -Name "Port-Status"
if ([string]::IsNullOrWhiteSpace($UpstreamSha))
{
    $UpstreamSha = $headUpstreamSha
}
if ($UpstreamSha -ne $headUpstreamSha)
{
    throw "HEAD records Java SHA $headUpstreamSha, not requested SHA $UpstreamSha."
}
if ($UpstreamSha -notmatch "^[0-9a-fA-F]{40}$")
{
    throw "HEAD has an invalid Upstream-Java-SHA trailer: $UpstreamSha"
}
$allowedStatuses = @("ported", "direct-data", "not-applicable", "blocked")
if ($headPortStatus -notin $allowedStatuses)
{
    throw "HEAD has an invalid Port-Status trailer: $headPortStatus"
}
if ($headPortStatus -ne "blocked" -and $state.lastCompletedJavaCommit -ne $UpstreamSha)
{
    throw "State records $($state.lastCompletedJavaCommit), but HEAD completed $UpstreamSha."
}
if ($headPortStatus -eq "blocked" -and $state.lastCompletedJavaCommit -eq $UpstreamSha)
{
    throw "A blocked Java commit must not advance lastCompletedJavaCommit."
}

$ledgerStatus = switch ($headPortStatus)
{
    "ported" { "Ported" }
    "direct-data" { "Direct data carryover" }
    "not-applicable" { "Not applicable" }
    "blocked" { "Blocked" }
}
$shortSha = $UpstreamSha.Substring(0, 9)
$ledgerMatches = @(
    Get-Content -LiteralPath (Join-Path $csharpRoot "docs\upstream-port-log.md") |
        Where-Object { $_ -match "^\|\s*\x60?$([regex]::Escape($shortSha))\x60?\s*\|" }
)
if ($ledgerMatches.Count -ne 1)
{
    throw "Expected one ledger row for $shortSha; found $($ledgerMatches.Count)."
}
if ($ledgerMatches[0] -notmatch "\|\s*$([regex]::Escape($ledgerStatus))\s*\|")
{
    throw "Ledger status for $shortSha does not match '$ledgerStatus'."
}

$completedSha = [string]$state.lastCompletedJavaCommit
$expectedJavaShas = @(
    (Invoke-GitCommand -Repository $javaRoot -Arguments @(
        "rev-list",
        "--reverse",
        "--topo-order",
        "$($state.baselineJavaCommit)..$completedSha"
    )).Lines | Where-Object { $_ -match "^[0-9a-fA-F]{40}$" }
)
$allLedgerLines = @(Get-Content -LiteralPath (Join-Path $csharpRoot "docs\upstream-port-log.md"))
$missingMappings = [System.Collections.Generic.List[string]]::new()
$duplicateMappings = [System.Collections.Generic.List[string]]::new()
$missingLedgerRows = [System.Collections.Generic.List[string]]::new()
foreach ($javaSha in $expectedJavaShas)
{
    $expectedShortSha = $javaSha.Substring(0, 9)
    $completedLedgerRows = @(
        $allLedgerLines | Where-Object {
            $_ -match "^\|\s*\x60?$([regex]::Escape($expectedShortSha))\x60?\s*\|"
        }
    )
    if ($completedLedgerRows.Count -ne 1)
    {
        $missingLedgerRows.Add($javaSha)
    }

    $candidates = @(
        (Invoke-GitCommand -Repository $csharpRoot -Arguments @(
            "log",
            "--format=%H",
            "--fixed-strings",
            "--grep=Upstream-Java-SHA: $javaSha"
        )).Lines | Where-Object { $_ -match "^[0-9a-fA-F]{40}$" }
    )
    $exactMatches = @(
        $candidates | Where-Object {
            $candidateMessage = (Invoke-GitCommand -Repository $csharpRoot -Arguments @(
                "show", "-s", "--format=%B", $_
            )).Text
            $hasExactSha = @(
                $candidateMessage -split "\r?\n" |
                    Where-Object { $_ -eq "Upstream-Java-SHA: $javaSha" }
            ).Count -eq 1
            $isCompleted = @(
                $candidateMessage -split "\r?\n" |
                    Where-Object { $_ -match "^Port-Status:\s*(ported|direct-data|not-applicable)\s*$" }
            ).Count -eq 1
            $hasExactSha -and $isCompleted
        }
    )
    if ($exactMatches.Count -eq 0)
    {
        $missingMappings.Add($javaSha)
    }
    elseif ($exactMatches.Count -gt 1)
    {
        $duplicateMappings.Add($javaSha)
    }
}
if ($missingMappings.Count -gt 0)
{
    throw "Missing C# commit trailers for completed Java commits: $($missingMappings -join ', ')"
}
if ($duplicateMappings.Count -gt 0)
{
    throw "Duplicate C# commit mappings for Java commits: $($duplicateMappings -join ', ')"
}
if ($missingLedgerRows.Count -gt 0)
{
    throw "Missing or duplicate ledger rows for completed Java commits: $($missingLedgerRows -join ', ')"
}

$result = [pscustomobject][ordered]@{
    schemaVersion = 1
    status = "verified"
    upstreamSha = $UpstreamSha
    portStatus = $headPortStatus
    csharpCommit = $head
    verifiedCompletedMappings = $expectedJavaShas.Count
    lastCompletedJavaCommit = $completedSha
}
if ($OutputFormat -eq "Json")
{
    Write-Output (ConvertTo-AutomationJson -InputObject $result -Compress)
}
else
{
    Write-Output "Verified C# commit $head for Java $UpstreamSha ($headPortStatus)."
    Write-Output "Completed Java mappings verified: $($expectedJavaShas.Count)"
}
