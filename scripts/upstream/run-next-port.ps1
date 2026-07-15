#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$UpstreamSha,
    [string]$CSharpRepository,
    [string]$JavaRepository,
    [string]$Remote = "upstream",
    [string]$Branch,
    [string]$CodexCommand = "codex",
    [string]$Model = $env:BEYOND_AION_CODEX_MODEL,
    [ValidateRange(60, 86400)] [int]$CodexTimeoutSeconds = 10800,
    [switch]$NoFetch,
    [switch]$RetryBlocked,
    [ValidateSet("Json", "Text")] [string]$OutputFormat = "Json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "UpstreamAutomation.psm1") -Force

function Invoke-NextPort
{
    $scan = Get-UpstreamScan `
        -CSharpRepository $script:csharpRoot `
        -JavaRepository $script:javaRoot `
        -Remote $Remote `
        -Branch $Branch `
        -NoFetch:$NoFetch `
        -SkipPullRequests
    if ($scan.pendingCount -eq 0)
    {
        return [pscustomobject][ordered]@{
            schemaVersion = 1
            status = "no-pending"
            checkedUtc = $scan.checkedUtc
            remoteHead = $scan.remoteHead
            lastCompletedJavaCommit = $scan.lastCompletedJavaCommit
        }
    }

    $commit = $scan.nextPending
    if (-not [string]::IsNullOrWhiteSpace($UpstreamSha) -and $UpstreamSha -ne $commit.sha)
    {
        throw "Only the first pending Java commit can run. Expected $($commit.sha), received $UpstreamSha."
    }
    $script:targetSha = $commit.sha
    $shortSha = $commit.sha.Substring(0, 9)
    $blockedRows = @(
        Get-Content -LiteralPath (Join-Path $script:csharpRoot "docs/upstream-port-log.md") |
            Where-Object {
                $_ -match "^\|\s*\x60?$([regex]::Escape($shortSha))\x60?\s*\|" -and
                $_ -match "\|\s*Blocked\s*\|"
            }
    )
    if ($blockedRows.Count -gt 0 -and -not $RetryBlocked)
    {
        return [pscustomobject][ordered]@{
            schemaVersion = 1
            status = "blocked-existing"
            upstreamSha = $commit.sha
            subject = $commit.subject
            message = "The first pending commit is already recorded as Blocked. Resolve the prerequisite and run with -RetryBlocked."
        }
    }

    $null = Assert-CSharpMainWorktree -CSharpRepository $script:csharpRoot -RequireClean
    $authCheck = Invoke-ExternalCommand `
        -FilePath $CodexCommand `
        -Arguments @("login", "status") `
        -WorkingDirectory $script:csharpRoot `
        -TimeoutSeconds 60 `
        -AllowFailure
    if ($authCheck.exitCode -ne 0 -or $authCheck.timedOut)
    {
        return [pscustomobject][ordered]@{
            schemaVersion = 1
            status = "codex-not-authenticated"
            upstreamSha = $commit.sha
            subject = $commit.subject
            message = "Codex CLI is not authenticated. Run scripts/automation/login-codex.ps1 and retry."
        }
    }
    $beforeHead = (Invoke-GitCommand -Repository $script:csharpRoot -Arguments @("rev-parse", "HEAD")).Text.Trim()
    $prepareOutput = @(& (Join-Path $PSScriptRoot "prepare-next.ps1") `
        -CSharpRepository $script:csharpRoot `
        -JavaRepository $script:javaRoot `
        -Remote $Remote `
        -Branch $Branch `
        -NoFetch `
        -SkipPullRequests `
        -OutputFormat Json)
    $prepared = ($prepareOutput | Select-Object -Last 1) | ConvertFrom-Json
    if ($prepared.status -ne "prepared" -or $prepared.upstreamSha -ne $commit.sha)
    {
        throw "The prepared package does not match Java commit $($commit.sha)."
    }
    $script:packagePath = [string]$prepared.packagePath

    $prompt = Get-Content -LiteralPath $prepared.promptPath -Raw
    $eventsPath = Join-Path $prepared.packagePath "codex-events.jsonl"
    $finalMessagePath = Join-Path $prepared.packagePath "codex-final.md"
    $processLogPath = Join-Path $prepared.packagePath "codex-process.log"
    $codexArguments = @(
        "--cd", $script:csharpRoot,
        "--sandbox", "workspace-write",
        "--ask-for-approval", "never",
        "--config", "sandbox_workspace_write.network_access=true"
    )
    if (-not [string]::IsNullOrWhiteSpace($Model))
    {
        $codexArguments += @("--model", $Model)
    }
    $codexArguments += @(
        "exec",
        "--json",
        "--ephemeral",
        "--output-last-message", $finalMessagePath,
        "-"
    )
    $execution = Invoke-ExternalCommand `
        -FilePath $CodexCommand `
        -Arguments $codexArguments `
        -WorkingDirectory $script:csharpRoot `
        -StandardInput $prompt `
        -TimeoutSeconds $CodexTimeoutSeconds `
        -LogPath $processLogPath `
        -AllowFailure
    $null = Write-Utf8File -Path $eventsPath -Content $execution.stdout
    if ($execution.exitCode -ne 0 -or $execution.timedOut)
    {
        return [pscustomobject][ordered]@{
            schemaVersion = 1
            status = if ($execution.timedOut) { "codex-timeout" } else { "codex-failed" }
            upstreamSha = $commit.sha
            subject = $commit.subject
            codexExitCode = $execution.exitCode
            codexTimedOut = $execution.timedOut
            packagePath = $prepared.packagePath
            processLogPath = $processLogPath
            eventsPath = $eventsPath
            worktree = Get-CSharpWorktreeState -CSharpRepository $script:csharpRoot
        }
    }

    $null = Assert-CSharpMainWorktree -CSharpRepository $script:csharpRoot -RequireClean
    $afterHead = (Invoke-GitCommand -Repository $script:csharpRoot -Arguments @("rev-parse", "HEAD")).Text.Trim()
    if ($afterHead -eq $beforeHead)
    {
        throw "Codex exited successfully without creating a C# commit."
    }
    $ancestor = Invoke-GitCommand -Repository $script:csharpRoot -Arguments @(
        "merge-base", "--is-ancestor", $beforeHead, $afterHead
    ) -AllowFailure
    if ($ancestor.ExitCode -ne 0)
    {
        throw "Codex rewrote or replaced the pre-run C# history."
    }
    $commitCount = [int](Invoke-GitCommand -Repository $script:csharpRoot -Arguments @(
        "rev-list", "--count", "$beforeHead..$afterHead"
    )).Text.Trim()
    if ($commitCount -ne 1)
    {
        throw "Codex must create exactly one C# commit; it created $commitCount."
    }

    $verifyOutput = @(& (Join-Path $PSScriptRoot "verify-port.ps1") `
        -UpstreamSha $commit.sha `
        -CSharpRepository $script:csharpRoot `
        -JavaRepository $script:javaRoot `
        -OutputFormat Json)
    $verification = ($verifyOutput | Select-Object -Last 1) | ConvertFrom-Json
    if ($verification.status -ne "verified")
    {
        throw "Post-Codex verification did not return a verified result."
    }

    return [pscustomobject][ordered]@{
        schemaVersion = 1
        status = if ($verification.portStatus -eq "blocked") { "blocked" } else { "committed" }
        upstreamSha = $commit.sha
        subject = $commit.subject
        portStatus = $verification.portStatus
        csharpCommit = $afterHead
        packagePath = $prepared.packagePath
        finalMessagePath = $finalMessagePath
        eventsPath = $eventsPath
        verification = $verification
    }
}

$csharpRoot = Get-CSharpRepositoryRoot -Path $CSharpRepository
$javaRoot = Get-JavaRepositoryRoot -CSharpRepository $csharpRoot -Path $JavaRepository
$artifactsRoot = Join-Path $csharpRoot "artifacts/upstream"
$null = New-Item -ItemType Directory -Path $artifactsRoot -Force
$lockPath = Join-Path $artifactsRoot "automation.lock"
$lockStream = $null
$result = $null
$exitCode = 0
$packagePath = $null
$targetSha = $null
try
{
    try
    {
        $lockStream = [System.IO.File]::Open(
            $lockPath,
            [System.IO.FileMode]::OpenOrCreate,
            [System.IO.FileAccess]::ReadWrite,
            [System.IO.FileShare]::None)
    }
    catch [System.IO.IOException]
    {
        $result = [pscustomobject][ordered]@{
            schemaVersion = 1
            status = "busy"
            message = "Another upstream port run owns the repository lock."
            lockPath = $lockPath
        }
    }

    if ($null -eq $result)
    {
        $result = Invoke-NextPort
        if ($result.status -in @("codex-not-authenticated", "codex-failed", "codex-timeout"))
        {
            $exitCode = 1
        }
    }
}
catch
{
    $exitCode = 1
    $result = [pscustomobject][ordered]@{
        schemaVersion = 1
        status = "failed"
        upstreamSha = $targetSha
        message = $_.Exception.Message
        packagePath = $packagePath
        worktree = Get-CSharpWorktreeState -CSharpRepository $csharpRoot
    }
}
finally
{
    if ($lockStream)
    {
        $lockStream.Dispose()
    }
}

$resultPath = if (-not [string]::IsNullOrWhiteSpace($packagePath)) {
    Join-Path $packagePath "runner-result.json"
} else {
    Join-Path $artifactsRoot "latest-run.json"
}
$null = Write-AutomationJson -InputObject $result -Path $resultPath
if ($OutputFormat -eq "Text")
{
    Write-Output "Upstream automation status: $($result.status)"
    if ($result.PSObject.Properties["upstreamSha"] -and $result.upstreamSha)
    {
        Write-Output "Java commit: $($result.upstreamSha)"
    }
    if ($result.PSObject.Properties["csharpCommit"] -and $result.csharpCommit)
    {
        Write-Output "C# commit: $($result.csharpCommit)"
    }
    if ($result.PSObject.Properties["message"] -and $result.message)
    {
        Write-Output $result.message
    }
    Write-Output "Result: $resultPath"
}
else
{
    Write-Output (ConvertTo-AutomationJson -InputObject $result -Compress)
}
exit $exitCode
