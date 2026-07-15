#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$UpstreamSha,
    [string]$CSharpRepository,
    [string]$JavaRepository,
    [string]$Remote = "upstream",
    [string]$Branch,
    [string]$FocusedTestProject,
    [string]$TestFilter,
    [switch]$NoRestore,
    [switch]$SkipFidelity,
    [switch]$NoFetch,
    [string]$OutputPath,
    [ValidateSet("Json", "Text")] [string]$OutputFormat = "Json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "UpstreamAutomation.psm1") -Force

$csharpRoot = Get-CSharpRepositoryRoot -Path $CSharpRepository
$javaRoot = Get-JavaRepositoryRoot -CSharpRepository $csharpRoot -Path $JavaRepository
$null = Assert-CSharpMainWorktree -CSharpRepository $csharpRoot
$scan = Get-UpstreamScan `
    -CSharpRepository $csharpRoot `
    -JavaRepository $javaRoot `
    -Remote $Remote `
    -Branch $Branch `
    -NoFetch:$NoFetch `
    -SkipPullRequests
if ($scan.pendingCount -eq 0)
{
    throw "No merged Java commit is pending validation."
}

$commit = $scan.nextPending
if (-not [string]::IsNullOrWhiteSpace($UpstreamSha) -and $UpstreamSha -ne $commit.sha)
{
    throw "Only the first pending Java commit can be validated. Expected $($commit.sha), received $UpstreamSha."
}
$UpstreamSha = $commit.sha

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

if ([string]::IsNullOrWhiteSpace($OutputPath))
{
    $OutputPath = Join-Path $packagePath "validation.json"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputPath))
{
    $OutputPath = Join-Path $csharpRoot $OutputPath
}
$logsPath = Join-Path $packagePath "validation-logs"
$null = New-Item -ItemType Directory -Path $logsPath -Force

$specifications = [System.Collections.Generic.List[object]]::new()
$specifications.Add([pscustomobject]@{
    Name = "git-diff-check"
    FilePath = "git"
    Arguments = @("diff", "HEAD", "--check", "--")
    Environment = $null
})
if (-not $NoRestore)
{
    $specifications.Add([pscustomobject]@{
        Name = "dotnet-restore"
        FilePath = "dotnet"
        Arguments = @("restore", "AionServer.slnx")
        Environment = $null
    })
}
if (-not [string]::IsNullOrWhiteSpace($FocusedTestProject))
{
    $focusedArguments = @("test", $FocusedTestProject, "--no-restore", "-v:minimal")
    if (-not [string]::IsNullOrWhiteSpace($TestFilter))
    {
        $focusedArguments += @("--filter", $TestFilter)
    }
    $specifications.Add([pscustomobject]@{
        Name = "focused-tests"
        FilePath = "dotnet"
        Arguments = $focusedArguments
        Environment = $null
    })
}
$specifications.Add([pscustomobject]@{
    Name = "warning-baseline-rebuild"
    FilePath = "pwsh"
    Arguments = @(
        "-NoLogo",
        "-NoProfile",
        "-NonInteractive",
        "-File",
        (Join-Path $csharpRoot "scripts/ci/check-warning-baseline.ps1"),
        "-NoRestore"
    )
    Environment = $null
})
$specifications.Add([pscustomobject]@{
    Name = "full-solution-tests"
    FilePath = "dotnet"
    Arguments = @("test", "AionServer.slnx", "--no-build", "--no-restore", "-v:minimal")
    Environment = $null
})
if (-not $SkipFidelity)
{
    $specifications.Add([pscustomobject]@{
        Name = "structural-fidelity"
        FilePath = "python"
        Arguments = @("scripts/parity/check_fidelity.py")
        Environment = @{ BEYOND_AION_JAVA_ROOT = $javaRoot }
    })
}

$startedUtc = [DateTime]::UtcNow
$steps = [System.Collections.Generic.List[object]]::new()
$failedStep = $null
foreach ($specification in $specifications)
{
    $logPath = Join-Path $logsPath "$($specification.Name).log"
    $execution = Invoke-ExternalCommand `
        -FilePath $specification.FilePath `
        -Arguments $specification.Arguments `
        -WorkingDirectory $csharpRoot `
        -Environment $specification.Environment `
        -LogPath $logPath `
        -AllowFailure
    $steps.Add([pscustomobject][ordered]@{
        name = $specification.Name
        command = $execution.command
        exitCode = $execution.exitCode
        durationSeconds = $execution.durationSeconds
        logPath = $execution.logPath
    })
    if ($execution.exitCode -ne 0)
    {
        $failedStep = $specification.Name
        break
    }
}

$fingerprint = Get-CSharpWorktreeFingerprint -CSharpRepository $csharpRoot
$finishedUtc = [DateTime]::UtcNow
$report = [pscustomobject][ordered]@{
    schemaVersion = 1
    status = if ($null -eq $failedStep) { "passed" } else { "failed" }
    upstreamSha = $UpstreamSha
    subject = $commit.subject
    csharpHead = (Invoke-GitCommand -Repository $csharpRoot -Arguments @("rev-parse", "HEAD")).Text.Trim()
    worktreeFingerprint = $fingerprint.sha256
    worktreeStatus = $fingerprint.status
    startedUtc = $startedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")
    finishedUtc = $finishedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")
    durationSeconds = [Math]::Round(($finishedUtc - $startedUtc).TotalSeconds, 3)
    failedStep = $failedStep
    steps = [object[]]$steps
}
$writtenPath = Write-AutomationJson -InputObject $report -Path $OutputPath

if ($OutputFormat -eq "Text")
{
    Write-Output "Validation $($report.status) for $UpstreamSha in $($report.durationSeconds)s."
    foreach ($step in $report.steps)
    {
        Write-Output "$($step.name): exit $($step.exitCode), $($step.durationSeconds)s ($($step.logPath))"
    }
    Write-Output "Report: $writtenPath"
}
else
{
    Write-Output (ConvertTo-AutomationJson -InputObject $report -Compress)
}
if ($report.status -ne "passed")
{
    exit 1
}
