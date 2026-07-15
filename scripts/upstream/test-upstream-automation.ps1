#requires -Version 7.2

[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Import-Module (Join-Path $PSScriptRoot "UpstreamAutomation.psm1") -Force

$sourceRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("BeyondAionSharp-upstream-test-" + [guid]::NewGuid().ToString("N"))
$utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Write-TestFile([string]$Path, [string]$Content)
{
    $directory = Split-Path $Path -Parent
    if (-not (Test-Path -LiteralPath $directory -PathType Container))
    {
        $null = New-Item -ItemType Directory -Path $directory -Force
    }
    [System.IO.File]::WriteAllText($Path, $Content, $utf8NoBom)
}

function Assert-Equal($Expected, $Actual, [string]$Message)
{
    if ($Expected -ne $Actual)
    {
        throw "$Message Expected '$Expected', actual '$Actual'."
    }
}

function Assert-True([bool]$Condition, [string]$Message)
{
    if (-not $Condition)
    {
        throw $Message
    }
}

function Invoke-TestScript([string]$Path, [hashtable]$Parameters)
{
    return & $Path @Parameters
}

function Commit-TrackerDecision([string]$Repository, [string]$MessagePath)
{
    $null = Invoke-GitCommand -Repository $Repository -Arguments @(
        "add",
        "docs/upstream-port-log.md",
        "docs/upstream-port-state.json"
    )
    $null = Invoke-GitCommand -Repository $Repository -Arguments @("commit", "-F", $MessagePath)
}

try
{
    $null = New-Item -ItemType Directory -Path $tempRoot -Force
    $javaRemote = Join-Path $tempRoot "java-remote.git"
    $javaRoot = Join-Path $tempRoot "java"
    $csharpRoot = Join-Path $tempRoot "csharp"
    $null = Invoke-ExternalCommand -FilePath git -Arguments @("init", "--bare", $javaRemote) -WorkingDirectory $tempRoot
    $null = New-Item -ItemType Directory -Path $javaRoot -Force
    $null = Invoke-ExternalCommand -FilePath git -Arguments @("init", "--initial-branch=4.8") -WorkingDirectory $javaRoot
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("config", "user.name", "Automation Test")
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("config", "user.email", "automation@example.invalid")

    Write-TestFile -Path (Join-Path $javaRoot "server.txt") -Content "baseline`n"
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("add", "server.txt")
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("commit", "-m", "Baseline")
    $baselineSha = (Invoke-GitCommand -Repository $javaRoot -Arguments @("rev-parse", "HEAD")).Text

    Write-TestFile -Path (Join-Path $javaRoot "server.txt") -Content "baseline`nfirst fix`n"
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("add", "server.txt")
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("commit", "-m", "First fix")
    $firstSha = (Invoke-GitCommand -Repository $javaRoot -Arguments @("rev-parse", "HEAD")).Text

    Write-TestFile -Path (Join-Path $javaRoot "data.xml") -Content "<data value=`"second`" />`n"
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("add", "data.xml")
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("commit", "-m", "Second data fix")
    $secondSha = (Invoke-GitCommand -Repository $javaRoot -Arguments @("rev-parse", "HEAD")).Text
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("remote", "add", "upstream", $javaRemote)
    $null = Invoke-GitCommand -Repository $javaRoot -Arguments @("push", "-u", "upstream", "4.8")

    $null = New-Item -ItemType Directory -Path $csharpRoot -Force
    $null = Invoke-ExternalCommand -FilePath git -Arguments @("init", "--initial-branch=main") -WorkingDirectory $csharpRoot
    $null = Invoke-GitCommand -Repository $csharpRoot -Arguments @("config", "user.name", "Automation Test")
    $null = Invoke-GitCommand -Repository $csharpRoot -Arguments @("config", "user.email", "automation@example.invalid")
    $state = [ordered]@{
        upstreamRepository = "https://github.com/example/java.git"
        upstreamBranch = "4.8"
        baselineJavaCommit = $baselineSha
        lastCompletedJavaCommit = $baselineSha
        lastScannedJavaCommit = $baselineSha
        updatedUtc = "2026-01-01T00:00:00Z"
    }
    Write-TestFile -Path (Join-Path $csharpRoot "docs/upstream-port-state.json") -Content (($state | ConvertTo-Json) + "`n")
    Write-TestFile -Path (Join-Path $csharpRoot "docs/upstream-port-log.md") -Content @"
# Upstream port log

| Upstream SHA | Date | Subject | Status | C# commit / PR | Notes |
|---|---|---|---|---|---|
"@
    Write-TestFile -Path (Join-Path $csharpRoot "docs/prompts/port-upstream-commit.md") -Content @"
Port {{UPSTREAM_SHA}}

{{UPSTREAM_PATCH}}
"@
    Write-TestFile -Path (Join-Path $csharpRoot ".gitignore") -Content "/artifacts/upstream/`n"
    $null = Invoke-GitCommand -Repository $csharpRoot -Arguments @("add", ".")
    $null = Invoke-GitCommand -Repository $csharpRoot -Arguments @("commit", "-m", "Fixture baseline")

    $common = @{
        CSharpRepository = $csharpRoot
        JavaRepository = $javaRoot
        NoFetch = $true
    }
    $scanJson = Invoke-TestScript -Path (Join-Path $PSScriptRoot "scan-upstream.ps1") -Parameters ($common + @{
        SkipPullRequests = $true
        OutputFormat = "Json"
    })
    $scan = $scanJson | ConvertFrom-Json
    Assert-Equal 2 $scan.pendingCount "The scan must return both pending commits."
    Assert-Equal $firstSha $scan.nextPending.sha "The scan must preserve Java history order."

    $preparedJson = Invoke-TestScript -Path (Join-Path $PSScriptRoot "prepare-next.ps1") -Parameters ($common + @{
        SkipPullRequests = $true
        OutputFormat = "Json"
    })
    $prepared = $preparedJson | ConvertFrom-Json
    Assert-Equal "prepared" $prepared.status "The first commit must be packaged."
    Assert-Equal $firstSha $prepared.upstreamSha "The package must target only the first commit."
    Assert-True (Test-Path -LiteralPath $prepared.promptPath -PathType Leaf) "The generated prompt is missing."
    Assert-True ((Get-Content -LiteralPath $prepared.promptPath -Raw).Contains($firstSha)) "The prompt does not contain the Java SHA."

    $validationRun = Invoke-ExternalCommand -FilePath pwsh -Arguments @(
        "-NoLogo",
        "-NoProfile",
        "-NonInteractive",
        "-File",
        (Join-Path $PSScriptRoot "validate-port.ps1"),
        "-CSharpRepository",
        $csharpRoot,
        "-JavaRepository",
        $javaRoot,
        "-NoFetch",
        "-NoRestore",
        "-SkipFidelity",
        "-OutputFormat",
        "Json"
    ) -WorkingDirectory $sourceRoot -AllowFailure
    Assert-True ($validationRun.exitCode -ne 0) "The fake repository should produce a failed validation result."
    $failedValidation = Get-Content -LiteralPath (Join-Path $prepared.packagePath "validation.json") -Raw | ConvertFrom-Json
    Assert-Equal "failed" $failedValidation.status "Validation failures must still produce a report."
    Assert-Equal "warning-baseline-rebuild" $failedValidation.failedStep "The first failing validation step was not recorded."

    $null = Invoke-GitCommand -Repository $csharpRoot -Arguments @("branch", "unexpected")
    $branchGateFailed = $false
    try
    {
        $null = Invoke-TestScript -Path (Join-Path $PSScriptRoot "prepare-next.ps1") -Parameters ($common + @{
            SkipPullRequests = $true
            OutputFormat = "Json"
        })
    }
    catch
    {
        $branchGateFailed = $true
    }
    Assert-True $branchGateFailed "Package preparation must reject extra local branches."
    $null = Invoke-GitCommand -Repository $csharpRoot -Arguments @("branch", "-D", "unexpected")

    Write-TestFile -Path (Join-Path $csharpRoot "dirty.txt") -Content "dirty`n"
    $cleanGateFailed = $false
    try
    {
        $null = Invoke-TestScript -Path (Join-Path $PSScriptRoot "prepare-next.ps1") -Parameters ($common + @{
            SkipPullRequests = $true
            OutputFormat = "Json"
        })
    }
    catch
    {
        $cleanGateFailed = $true
    }
    Assert-True $cleanGateFailed "Package preparation must reject a dirty C# worktree."
    Remove-Item -LiteralPath (Join-Path $csharpRoot "dirty.txt") -Force

    $unauthenticatedRun = Invoke-ExternalCommand -FilePath pwsh -Arguments @(
        "-NoLogo",
        "-NoProfile",
        "-NonInteractive",
        "-File",
        (Join-Path $PSScriptRoot "run-next-port.ps1"),
        "-CSharpRepository",
        $csharpRoot,
        "-JavaRepository",
        $javaRoot,
        "-CodexCommand",
        "git",
        "-NoFetch",
        "-OutputFormat",
        "Json"
    ) -WorkingDirectory $sourceRoot -AllowFailure
    Assert-True ($unauthenticatedRun.exitCode -ne 0) "An unauthenticated Codex preflight must fail the runner."
    $unauthenticated = $unauthenticatedRun.stdout.Trim() | ConvertFrom-Json
    Assert-Equal "codex-not-authenticated" $unauthenticated.status "The runner did not report its authentication failure."

    $blockedJson = Invoke-TestScript -Path (Join-Path $PSScriptRoot "complete-port.ps1") -Parameters ($common + @{
        Status = "blocked"
        Notes = "Missing prerequisite in the C# fixture."
        OutputFormat = "Json"
    })
    $blocked = $blockedJson | ConvertFrom-Json
    Assert-True (-not $blocked.advanced) "A blocked decision must not advance the state."
    Assert-Equal $baselineSha $blocked.lastCompletedJavaCommit "Blocked state advanced unexpectedly."
    Commit-TrackerDecision -Repository $csharpRoot -MessagePath $blocked.commitMessagePath
    $null = Invoke-TestScript -Path (Join-Path $PSScriptRoot "verify-port.ps1") -Parameters @{
        CSharpRepository = $csharpRoot
        JavaRepository = $javaRoot
        OutputFormat = "Json"
    }

    $firstJson = Invoke-TestScript -Path (Join-Path $PSScriptRoot "complete-port.ps1") -Parameters ($common + @{
        Status = "not-applicable"
        Notes = "Fixture records an evidence-based non-applicable decision."
        OutputFormat = "Json"
    })
    $first = $firstJson | ConvertFrom-Json
    Assert-True $first.advanced "A non-applicable decision must advance the state."
    Commit-TrackerDecision -Repository $csharpRoot -MessagePath $first.commitMessagePath
    $firstVerification = Invoke-TestScript -Path (Join-Path $PSScriptRoot "verify-port.ps1") -Parameters @{
        CSharpRepository = $csharpRoot
        JavaRepository = $javaRoot
        OutputFormat = "Json"
    } | ConvertFrom-Json
    Assert-Equal $firstSha $firstVerification.upstreamSha "The first decision did not verify."

    $secondPrepared = Invoke-TestScript -Path (Join-Path $PSScriptRoot "prepare-next.ps1") -Parameters ($common + @{
        SkipPullRequests = $true
        OutputFormat = "Json"
    }) | ConvertFrom-Json
    Assert-Equal $secondSha $secondPrepared.upstreamSha "The second package was not selected after advancement."
    Write-TestFile -Path (Join-Path $csharpRoot "ported-data.xml") -Content "<ported value=`"second`" />`n"
    $secondFingerprint = Get-CSharpWorktreeFingerprint -CSharpRepository $csharpRoot
    $secondValidation = [ordered]@{
        schemaVersion = 1
        status = "passed"
        upstreamSha = $secondSha
        csharpHead = (Invoke-GitCommand -Repository $csharpRoot -Arguments @("rev-parse", "HEAD")).Text
        worktreeFingerprint = $secondFingerprint.sha256
    }
    Write-TestFile -Path (Join-Path $secondPrepared.packagePath "validation.json") -Content (($secondValidation | ConvertTo-Json) + "`n")
    $secondJson = Invoke-TestScript -Path (Join-Path $PSScriptRoot "complete-port.ps1") -Parameters ($common + @{
        Status = "direct-data"
        Notes = "Second fixture data carryover."
        OutputFormat = "Json"
    })
    $second = $secondJson | ConvertFrom-Json
    $null = Invoke-GitCommand -Repository $csharpRoot -Arguments @("add", "ported-data.xml")
    Commit-TrackerDecision -Repository $csharpRoot -MessagePath $second.commitMessagePath
    $secondVerification = Invoke-TestScript -Path (Join-Path $PSScriptRoot "verify-port.ps1") -Parameters @{
        CSharpRepository = $csharpRoot
        JavaRepository = $javaRoot
        OutputFormat = "Json"
    } | ConvertFrom-Json
    Assert-Equal 2 $secondVerification.verifiedCompletedMappings "Both completed mappings must verify."

    $finalScan = Invoke-TestScript -Path (Join-Path $PSScriptRoot "scan-upstream.ps1") -Parameters ($common + @{
        SkipPullRequests = $true
        OutputFormat = "Json"
    }) | ConvertFrom-Json
    Assert-Equal 0 $finalScan.pendingCount "The final queue must be empty."
    Write-Output "Upstream automation self-test passed."
}
finally
{
    if (Test-Path -LiteralPath $tempRoot -PathType Container)
    {
        $resolvedTemp = [System.IO.Path]::GetFullPath($tempRoot)
        $tempPrefix = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd(
            [System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
        $leaf = Split-Path $resolvedTemp -Leaf
        if (-not $resolvedTemp.StartsWith($tempPrefix, [StringComparison]::OrdinalIgnoreCase) -or
            -not $leaf.StartsWith("BeyondAionSharp-upstream-test-", [StringComparison]::Ordinal))
        {
            throw "Refusing to remove unexpected test directory: $resolvedTemp"
        }
        Remove-Item -LiteralPath $resolvedTemp -Recurse -Force
    }
}
