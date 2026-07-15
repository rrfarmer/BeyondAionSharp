#requires -Version 7.2

[CmdletBinding()]
param(
    [string]$CSharpRepository,
    [string]$JavaRepository,
    [string]$Remote = "upstream",
    [string]$Branch,
    [string]$ArtifactsRoot = "artifacts\upstream",
    [switch]$NoFetch,
    [switch]$SkipPullRequests,
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

if ($scan.pendingCount -eq 0)
{
    $result = [pscustomobject][ordered]@{
        schemaVersion = 1
        status = "no-pending"
        checkedUtc = $scan.checkedUtc
        remoteHead = $scan.remoteHead
        lastCompletedJavaCommit = $scan.lastCompletedJavaCommit
        packagePath = $null
    }
}
else
{
    $null = Assert-CSharpMainWorktree -CSharpRepository $csharpRoot -RequireClean
    $commit = $scan.nextPending
    if ([string]::IsNullOrWhiteSpace($commit.parentSha))
    {
        throw "Cannot package root Java commit $($commit.sha); a parent commit is required."
    }

    $resolvedArtifactsRoot = if ([System.IO.Path]::IsPathRooted($ArtifactsRoot)) {
        [System.IO.Path]::GetFullPath($ArtifactsRoot)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $csharpRoot $ArtifactsRoot))
    }
    $slug = ConvertTo-PortSlug -Subject $commit.subject
    $packagePath = Join-Path $resolvedArtifactsRoot "$($commit.shortSha)-$slug"
    $null = New-Item -ItemType Directory -Path $packagePath -Force

    $commitPatchPath = Join-Path $packagePath "commit.patch"
    $diffPatchPath = Join-Path $packagePath "diff.patch"
    $null = Invoke-GitToFile -Repository $javaRoot -Arguments @(
        "show",
        "--format=fuller",
        "--binary",
        "--find-renames",
        $commit.sha
    ) -OutputPath $commitPatchPath
    $null = Invoke-GitToFile -Repository $javaRoot -Arguments @(
        "diff",
        "--binary",
        "--find-renames",
        $commit.parentSha,
        $commit.sha,
        "--"
    ) -OutputPath $diffPatchPath

    $metadata = [pscustomobject][ordered]@{
        schemaVersion = 1
        promptVersion = 2
        preparedUtc = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        upstreamRepository = $scan.upstreamRepository
        upstreamBranch = $scan.branch
        upstreamSha = $commit.sha
        parentSha = $commit.parentSha
        subject = $commit.subject
        date = $commit.date
        changedFiles = [object[]]$commit.changedFiles
        csharpBaseCommit = (Invoke-GitCommand -Repository $csharpRoot -Arguments @("rev-parse", "HEAD")).Text.Trim()
        csharpBranch = "main"
        packagePath = $packagePath
    }
    $metadataPath = Write-AutomationJson -InputObject $metadata -Path (Join-Path $packagePath "metadata.json")
    $null = Write-AutomationJson -InputObject $commit.changedFiles -Path (Join-Path $packagePath "changed-files.json")

    $promptTemplatePath = Join-Path $csharpRoot "docs\prompts\port-upstream-commit.md"
    if (-not (Test-Path -LiteralPath $promptTemplatePath -PathType Leaf))
    {
        throw "Port prompt template was not found: $promptTemplatePath"
    }
    $promptTemplate = Get-Content -LiteralPath $promptTemplatePath -Raw
    $patch = Get-Content -LiteralPath $commitPatchPath -Raw
    $prompt = $promptTemplate.Replace("{{UPSTREAM_SHA}}", $commit.sha).Replace("{{UPSTREAM_PATCH}}", $patch)
    $promptPath = Write-Utf8File -Path (Join-Path $packagePath "prompt.md") -Content $prompt

    $backtick = [char]96
    $readme = @"
# Port package $($commit.shortSha)

- Java commit: $backtick$($commit.sha)$backtick
- Parent: $backtick$($commit.parentSha)$backtick
- Subject: $($commit.subject)
- C# base: $backtick$($metadata.csharpBaseCommit)$backtick on ${backtick}main$backtick

Give ${backtick}prompt.md$backtick to the coding agent from the C# repository root. The agent must port only this Java commit. After implementation, run ${backtick}scripts/upstream/validate-port.ps1$backtick, then ${backtick}scripts/upstream/complete-port.ps1$backtick, commit once with the required trailers, and run ${backtick}scripts/upstream/verify-port.ps1$backtick.
"@
    $null = Write-Utf8File -Path (Join-Path $packagePath "README.md") -Content $readme

    $result = [pscustomobject][ordered]@{
        schemaVersion = 1
        status = "prepared"
        upstreamSha = $commit.sha
        shortSha = $commit.shortSha
        subject = $commit.subject
        packagePath = $packagePath
        metadataPath = $metadataPath
        promptPath = $promptPath
        commitPatchPath = $commitPatchPath
        diffPatchPath = $diffPatchPath
        csharpBaseCommit = $metadata.csharpBaseCommit
    }
}

if ($OutputFormat -eq "Text")
{
    if ($result.status -eq "no-pending")
    {
        Write-Output "No merged Java commits are pending. Upstream is $($result.remoteHead)."
    }
    else
    {
        Write-Output "Prepared $($result.upstreamSha): $($result.subject)"
        Write-Output "Package: $($result.packagePath)"
        Write-Output "Prompt: $($result.promptPath)"
    }
}
else
{
    Write-Output (ConvertTo-AutomationJson -InputObject $result -Compress)
}
