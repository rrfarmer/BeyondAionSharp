#requires -Version 7.2

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:Utf8NoBom = [System.Text.UTF8Encoding]::new($false)

function Get-CSharpRepositoryRoot
{
    param([string]$Path)

    $candidate = $Path
    if ([string]::IsNullOrWhiteSpace($candidate))
    {
        $candidate = $env:BEYOND_AION_CSHARP_ROOT
    }
    if ([string]::IsNullOrWhiteSpace($candidate))
    {
        $candidate = Join-Path $PSScriptRoot "..\.."
    }

    if (-not (Test-Path -LiteralPath $candidate -PathType Container))
    {
        throw "C# repository was not found: $candidate"
    }
    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $candidate).Path)
}

function Get-JavaRepositoryRoot
{
    param(
        [string]$CSharpRepository,
        [string]$Path
    )

    $candidate = $Path
    if ([string]::IsNullOrWhiteSpace($candidate))
    {
        $candidate = $env:BEYOND_AION_JAVA_ROOT
    }
    if ([string]::IsNullOrWhiteSpace($candidate))
    {
        $candidate = Join-Path (Split-Path $CSharpRepository -Parent) "aion-server"
    }

    if (-not (Test-Path -LiteralPath $candidate -PathType Container))
    {
        throw "Java repository was not found: $candidate"
    }
    return [System.IO.Path]::GetFullPath((Resolve-Path -LiteralPath $candidate).Path)
}

function Invoke-GitCommand
{
    param(
        [Parameter(Mandatory)] [string]$Repository,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [switch]$AllowFailure
    )

    if (-not (Get-Command git -ErrorAction SilentlyContinue))
    {
        throw "The git CLI was not found on PATH."
    }

    $git = (Get-Command git -ErrorAction Stop).Source
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $git
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add("-C")
    $startInfo.ArgumentList.Add($Repository)
    foreach ($argument in $Arguments)
    {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $null = $process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult().TrimEnd("`r", "`n")
    $stderr = $stderrTask.GetAwaiter().GetResult().TrimEnd("`r", "`n")
    $lines = if ([string]::IsNullOrEmpty($stdout)) { @() } else { @($stdout -split "\r?\n") }

    $result = [pscustomobject][ordered]@{
        ExitCode = $process.ExitCode
        Lines = [object[]]$lines
        Text = $stdout
        ErrorText = $stderr
    }
    if ($process.ExitCode -ne 0 -and -not $AllowFailure)
    {
        $command = "git -C `"$Repository`" " + ($Arguments -join " ")
        $details = @($stdout, $stderr) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        throw "$command failed with exit code $($process.ExitCode).`n$($details -join [Environment]::NewLine)"
    }
    return $result
}

function Assert-GitRepository
{
    param([Parameter(Mandatory)] [string]$Repository)

    $inside = Invoke-GitCommand -Repository $Repository -Arguments @("rev-parse", "--is-inside-work-tree")
    if (($inside.Text.Trim()) -ne "true")
    {
        throw "Not a Git worktree: $Repository"
    }
}

function Get-UpstreamPortState
{
    param([Parameter(Mandatory)] [string]$CSharpRepository)

    $statePath = Join-Path $CSharpRepository "docs\upstream-port-state.json"
    if (-not (Test-Path -LiteralPath $statePath -PathType Leaf))
    {
        throw "Upstream state file was not found: $statePath"
    }

    $state = Get-Content -LiteralPath $statePath -Raw | ConvertFrom-Json
    foreach ($property in @("upstreamRepository", "upstreamBranch", "baselineJavaCommit", "lastCompletedJavaCommit"))
    {
        if (-not $state.PSObject.Properties[$property] -or [string]::IsNullOrWhiteSpace($state.$property))
        {
            throw "Upstream state is missing '$property': $statePath"
        }
    }
    foreach ($property in @("baselineJavaCommit", "lastCompletedJavaCommit"))
    {
        if ($state.$property -notmatch "^[0-9a-fA-F]{40}$")
        {
            throw "Upstream state '$property' is not a 40-character SHA: $($state.$property)"
        }
    }
    return $state
}

function Get-CSharpWorktreeState
{
    param([Parameter(Mandatory)] [string]$CSharpRepository)

    $branch = (Invoke-GitCommand -Repository $CSharpRepository -Arguments @("branch", "--show-current")).Text.Trim()
    $branches = @(
        (Invoke-GitCommand -Repository $CSharpRepository -Arguments @(
            "for-each-ref",
            "refs/heads",
            "--format=%(refname:short)"
        )).Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
    $changes = @(
        (Invoke-GitCommand -Repository $CSharpRepository -Arguments @(
            "status",
            "--porcelain=v1",
            "--untracked-files=all"
        )).Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )

    return [pscustomobject][ordered]@{
        branch = $branch
        branches = [object[]]$branches
        clean = $changes.Count -eq 0
        changes = [object[]]$changes
    }
}

function Get-CSharpWorktreeFingerprint
{
    param([Parameter(Mandatory)] [string]$CSharpRepository)

    $status = @(
        (Invoke-GitCommand -Repository $CSharpRepository -Arguments @(
            "status",
            "--porcelain=v1",
            "--untracked-files=all"
        )).Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
    $trackedPaths = @(
        (Invoke-GitCommand -Repository $CSharpRepository -Arguments @(
            "-c",
            "core.quotepath=false",
            "diff",
            "--name-only",
            "HEAD",
            "--"
        )).Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )
    $untrackedPaths = @(
        (Invoke-GitCommand -Repository $CSharpRepository -Arguments @(
            "-c",
            "core.quotepath=false",
            "ls-files",
            "--others",
            "--exclude-standard"
        )).Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    )

    $repositoryPrefix = $CSharpRepository.TrimEnd([System.IO.Path]::DirectorySeparatorChar) +
        [System.IO.Path]::DirectorySeparatorChar
    $material = [System.Collections.Generic.List[string]]::new()
    foreach ($line in $status)
    {
        $material.Add("status:$line")
    }
    foreach ($relativePath in @($trackedPaths + $untrackedPaths | Sort-Object -Unique))
    {
        $fullPath = [System.IO.Path]::GetFullPath((Join-Path $CSharpRepository $relativePath))
        if (-not $fullPath.StartsWith($repositoryPrefix, [StringComparison]::OrdinalIgnoreCase))
        {
            throw "Git returned a path outside the C# repository: $relativePath"
        }

        $fileHash = if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        } else {
            "missing"
        }
        $material.Add("file:${relativePath}:$fileHash")
    }

    $payload = $material -join "`n"
    $hashBytes = [System.Security.Cryptography.SHA256]::HashData(
        [System.Text.Encoding]::UTF8.GetBytes($payload))
    return [pscustomobject][ordered]@{
        sha256 = [Convert]::ToHexString($hashBytes).ToLowerInvariant()
        status = [object[]]$status
        paths = [object[]]@($trackedPaths + $untrackedPaths | Sort-Object -Unique)
    }
}

function Assert-CSharpMainWorktree
{
    param(
        [Parameter(Mandatory)] [string]$CSharpRepository,
        [string]$ExpectedBranch = "main",
        [switch]$RequireClean
    )

    $worktree = Get-CSharpWorktreeState -CSharpRepository $CSharpRepository
    if ($worktree.branch -ne $ExpectedBranch)
    {
        throw "C# worktree must be on '$ExpectedBranch'; current branch is '$($worktree.branch)'."
    }
    if ($worktree.branches.Count -ne 1 -or $worktree.branches[0] -ne $ExpectedBranch)
    {
        throw "C# repository must contain only the '$ExpectedBranch' branch. Found: $($worktree.branches -join ', ')"
    }
    if ($RequireClean -and -not $worktree.clean)
    {
        throw "C# worktree must be clean before preparing a port package:`n$($worktree.changes -join [Environment]::NewLine)"
    }
    return $worktree
}

function Get-GitHubRepositoryName
{
    param([Parameter(Mandatory)] [string]$RepositoryUrl)

    if ($RepositoryUrl -match "github\.com[/:](?<name>[^/]+/[^/]+?)(?:\.git)?$")
    {
        return $Matches.name
    }
    throw "Cannot derive a GitHub owner/repository name from: $RepositoryUrl"
}

function Get-OpenPullRequests
{
    param(
        [Parameter(Mandatory)] [string]$GitHubRepository,
        [Parameter(Mandatory)] [string]$BaseBranch
    )

    $headers = @{
        Accept = "application/vnd.github+json"
        "X-GitHub-Api-Version" = "2022-11-28"
        "User-Agent" = "BeyondAionSharp-upstream-monitor"
    }
    if (-not [string]::IsNullOrWhiteSpace($env:GITHUB_TOKEN))
    {
        $headers.Authorization = "Bearer $($env:GITHUB_TOKEN)"
    }

    $encodedBranch = [System.Uri]::EscapeDataString($BaseBranch)
    $uri = "https://api.github.com/repos/$GitHubRepository/pulls?state=open&base=$encodedBranch&per_page=100"
    # Invoke-RestMethod returns JSON arrays as one pipeline object in PowerShell 7.
    # Do not wrap the call in @(), which would create a nested object array.
    $response = Invoke-RestMethod -Uri $uri -Headers $headers -Method Get
    return [object[]]@(
        $response | Sort-Object number | ForEach-Object {
            [pscustomobject][ordered]@{
                number = [int]$_.number
                title = [string]$_.title
                url = [string]$_.html_url
                draft = [bool]$_.draft
                author = [string]$_.user.login
                updatedUtc = ([DateTimeOffset]$_.updated_at).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
                headBranch = [string]$_.head.ref
                headSha = [string]$_.head.sha
                baseBranch = [string]$_.base.ref
            }
        }
    )
}

function Get-UpstreamScan
{
    param(
        [Parameter(Mandatory)] [string]$CSharpRepository,
        [Parameter(Mandatory)] [string]$JavaRepository,
        [string]$Remote = "upstream",
        [string]$Branch,
        [switch]$NoFetch,
        [switch]$SkipPullRequests
    )

    Assert-GitRepository -Repository $CSharpRepository
    Assert-GitRepository -Repository $JavaRepository
    $state = Get-UpstreamPortState -CSharpRepository $CSharpRepository
    if ([string]::IsNullOrWhiteSpace($Branch))
    {
        $Branch = [string]$state.upstreamBranch
    }

    if (-not $NoFetch)
    {
        $null = Invoke-GitCommand -Repository $JavaRepository -Arguments @("fetch", "--prune", $Remote, $Branch)
    }

    $remoteRef = "refs/remotes/$Remote/$Branch"
    $remoteHeadResult = Invoke-GitCommand -Repository $JavaRepository -Arguments @("rev-parse", "--verify", "$remoteRef^{commit}")
    $remoteHead = $remoteHeadResult.Text.Trim()
    $completedSha = [string]$state.lastCompletedJavaCommit

    $completedExists = Invoke-GitCommand -Repository $JavaRepository -Arguments @("cat-file", "-e", "$completedSha^{commit}") -AllowFailure
    if ($completedExists.ExitCode -ne 0)
    {
        throw "The completed Java commit is unavailable in the Java checkout: $completedSha"
    }
    $ancestor = Invoke-GitCommand -Repository $JavaRepository -Arguments @("merge-base", "--is-ancestor", $completedSha, $remoteHead) -AllowFailure
    if ($ancestor.ExitCode -eq 1)
    {
        throw "Completed Java commit $completedSha is not an ancestor of $remoteRef ($remoteHead). Refusing a non-linear scan."
    }
    if ($ancestor.ExitCode -ne 0)
    {
        throw "Unable to compare $completedSha with $remoteRef.`n$($ancestor.Text)"
    }

    $range = "$completedSha..$remoteRef"
    $pendingShas = @(
        (Invoke-GitCommand -Repository $JavaRepository -Arguments @("rev-list", "--reverse", "--topo-order", $range)).Lines |
            Where-Object { $_ -match "^[0-9a-f]{40}$" }
    )
    $pending = foreach ($sha in $pendingShas)
    {
        $metadata = (Invoke-GitCommand -Repository $JavaRepository -Arguments @(
            "show",
            "-s",
            "--format=%H%x1f%P%x1f%cI%x1f%cs%x1f%s",
            $sha
        )).Text
        $parts = $metadata.Split([char]0x1f)
        if ($parts.Count -lt 5)
        {
            throw "Could not parse metadata for Java commit $sha."
        }
        $parents = @($parts[1].Split(" ", [StringSplitOptions]::RemoveEmptyEntries))
        $changedFiles = if (@($parents).Count -gt 0) {
            @(
                (Invoke-GitCommand -Repository $JavaRepository -Arguments @(
                    "diff",
                    "--name-only",
                    "--find-renames",
                    $parents[0],
                    $sha,
                    "--"
                )).Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            )
        } else {
            @(
                (Invoke-GitCommand -Repository $JavaRepository -Arguments @(
                    "ls-tree",
                    "-r",
                    "--name-only",
                    $sha
                )).Lines | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
            )
        }

        [pscustomobject][ordered]@{
            sha = $parts[0]
            shortSha = $parts[0].Substring(0, 9)
            parentSha = if (@($parents).Count -gt 0) { @($parents)[0] } else { $null }
            parentShas = [object[]]@($parents)
            committedUtc = ([DateTimeOffset]$parts[2]).UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ssZ")
            date = $parts[3]
            subject = $parts[4].TrimEnd()
            changedFileCount = @($changedFiles).Count
            changedFiles = [object[]]@($changedFiles)
        }
    }

    $warnings = [System.Collections.Generic.List[string]]::new()
    $pullRequests = @()
    $githubRepository = $null
    if (-not $SkipPullRequests)
    {
        try
        {
            $githubRepository = Get-GitHubRepositoryName -RepositoryUrl ([string]$state.upstreamRepository)
            $pullRequests = @(Get-OpenPullRequests -GitHubRepository $githubRepository -BaseBranch $Branch)
        }
        catch
        {
            $warnings.Add("Open pull request lookup failed: $($_.Exception.Message)")
        }
    }

    return [pscustomobject][ordered]@{
        schemaVersion = 1
        checkedUtc = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
        csharpRepository = $CSharpRepository
        javaRepository = $JavaRepository
        upstreamRepository = [string]$state.upstreamRepository
        githubRepository = $githubRepository
        remote = $Remote
        branch = $Branch
        remoteRef = $remoteRef
        remoteHead = $remoteHead
        baselineJavaCommit = [string]$state.baselineJavaCommit
        lastCompletedJavaCommit = $completedSha
        pendingCount = @($pending).Count
        pending = [object[]]@($pending)
        nextPending = if (@($pending).Count -gt 0) { @($pending)[0] } else { $null }
        openPullRequestCount = @($pullRequests).Count
        openPullRequests = [object[]]@($pullRequests)
        warnings = [object[]]@($warnings)
    }
}

function Write-Utf8File
{
    param(
        [Parameter(Mandatory)] [string]$Path,
        [AllowEmptyString()] [string]$Content
    )

    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $directory = Split-Path $fullPath -Parent
    if (-not (Test-Path -LiteralPath $directory -PathType Container))
    {
        $null = New-Item -ItemType Directory -Path $directory -Force
    }
    [System.IO.File]::WriteAllText($fullPath, $Content, $script:Utf8NoBom)
    return $fullPath
}

function ConvertTo-AutomationJson
{
    param(
        [Parameter(Mandatory)] [object]$InputObject,
        [switch]$Compress
    )

    if ($Compress)
    {
        return $InputObject | ConvertTo-Json -Depth 30 -Compress
    }
    return $InputObject | ConvertTo-Json -Depth 30
}

function Write-AutomationJson
{
    param(
        [Parameter(Mandatory)] [object]$InputObject,
        [Parameter(Mandatory)] [string]$Path
    )

    $json = ConvertTo-AutomationJson -InputObject $InputObject
    return Write-Utf8File -Path $Path -Content ($json + [Environment]::NewLine)
}

function Invoke-GitToFile
{
    param(
        [Parameter(Mandatory)] [string]$Repository,
        [Parameter(Mandatory)] [string[]]$Arguments,
        [Parameter(Mandatory)] [string]$OutputPath
    )

    $fullOutputPath = [System.IO.Path]::GetFullPath($OutputPath)
    $directory = Split-Path $fullOutputPath -Parent
    if (-not (Test-Path -LiteralPath $directory -PathType Container))
    {
        $null = New-Item -ItemType Directory -Path $directory -Force
    }

    $git = (Get-Command git -ErrorAction Stop).Source
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $git
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add("-C")
    $startInfo.ArgumentList.Add($Repository)
    foreach ($argument in $Arguments)
    {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $null = $process.Start()
    $errorTask = $process.StandardError.ReadToEndAsync()
    $stream = [System.IO.File]::Create($fullOutputPath)
    try
    {
        $process.StandardOutput.BaseStream.CopyTo($stream)
    }
    finally
    {
        $stream.Dispose()
    }
    $process.WaitForExit()
    $errorText = $errorTask.GetAwaiter().GetResult()
    if ($process.ExitCode -ne 0)
    {
        Remove-Item -LiteralPath $fullOutputPath -Force -ErrorAction SilentlyContinue
        throw "git output command failed with exit code $($process.ExitCode): $errorText"
    }
    return $fullOutputPath
}

function Invoke-ExternalCommand
{
    param(
        [Parameter(Mandatory)] [string]$FilePath,
        [string[]]$Arguments = @(),
        [Parameter(Mandatory)] [string]$WorkingDirectory,
        [System.Collections.IDictionary]$Environment,
        [string]$LogPath,
        [switch]$AllowFailure
    )

    $resolvedCommand = Get-Command $FilePath -ErrorAction Stop
    $executable = $resolvedCommand.Source
    if ([string]::IsNullOrWhiteSpace($executable))
    {
        $executable = $resolvedCommand.Path
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $executable
    $startInfo.WorkingDirectory = $WorkingDirectory
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in $Arguments)
    {
        $startInfo.ArgumentList.Add($argument)
    }
    if ($Environment)
    {
        foreach ($name in $Environment.Keys)
        {
            $startInfo.Environment[[string]$name] = [string]$Environment[$name]
        }
    }

    $startedUtc = [DateTime]::UtcNow
    $process = [System.Diagnostics.Process]::new()
    $process.StartInfo = $startInfo
    $null = $process.Start()
    $stdoutTask = $process.StandardOutput.ReadToEndAsync()
    $stderrTask = $process.StandardError.ReadToEndAsync()
    $process.WaitForExit()
    $stdout = $stdoutTask.GetAwaiter().GetResult()
    $stderr = $stderrTask.GetAwaiter().GetResult()
    $finishedUtc = [DateTime]::UtcNow

    $displayArguments = @($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') { '"' + ($_ -replace '"', '\"') + '"' } else { $_ }
    })
    $commandLine = $FilePath + $(if ($displayArguments.Count -gt 0) {
        " " + ($displayArguments -join " ")
    } else {
        ""
    })

    if (-not [string]::IsNullOrWhiteSpace($LogPath))
    {
        $log = @(
            "Command: $commandLine",
            "Working directory: $WorkingDirectory",
            "Started UTC: $($startedUtc.ToString('yyyy-MM-ddTHH:mm:ssZ'))",
            "Finished UTC: $($finishedUtc.ToString('yyyy-MM-ddTHH:mm:ssZ'))",
            "Exit code: $($process.ExitCode)",
            "",
            "--- stdout ---",
            $stdout.TrimEnd(),
            "",
            "--- stderr ---",
            $stderr.TrimEnd(),
            ""
        ) -join [Environment]::NewLine
        $null = Write-Utf8File -Path $LogPath -Content $log
    }

    $result = [pscustomobject][ordered]@{
        command = $commandLine
        exitCode = $process.ExitCode
        startedUtc = $startedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")
        finishedUtc = $finishedUtc.ToString("yyyy-MM-ddTHH:mm:ssZ")
        durationSeconds = [Math]::Round(($finishedUtc - $startedUtc).TotalSeconds, 3)
        stdout = $stdout
        stderr = $stderr
        logPath = $LogPath
    }
    if ($process.ExitCode -ne 0 -and -not $AllowFailure)
    {
        throw "$commandLine failed with exit code $($process.ExitCode). See $LogPath"
    }
    return $result
}

function ConvertTo-PortSlug
{
    param([Parameter(Mandatory)] [string]$Subject)

    $slug = $Subject.ToLowerInvariant() -replace "[^a-z0-9]+", "-"
    $slug = $slug.Trim("-")
    if ($slug.Length -gt 48)
    {
        $slug = $slug.Substring(0, 48).TrimEnd("-")
    }
    if ([string]::IsNullOrWhiteSpace($slug))
    {
        return "upstream-change"
    }
    return $slug
}

Export-ModuleMember -Function @(
    "Assert-CSharpMainWorktree",
    "Assert-GitRepository",
    "ConvertTo-AutomationJson",
    "ConvertTo-PortSlug",
    "Get-CSharpRepositoryRoot",
    "Get-CSharpWorktreeFingerprint",
    "Get-CSharpWorktreeState",
    "Get-JavaRepositoryRoot",
    "Get-UpstreamPortState",
    "Get-UpstreamScan",
    "Invoke-GitCommand",
    "Invoke-GitToFile",
    "Invoke-ExternalCommand",
    "Write-AutomationJson",
    "Write-Utf8File"
)
