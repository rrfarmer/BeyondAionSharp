param(
	[string]$Remote = "java-upstream",
	[string]$Branch = "4.8"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$state = Get-Content (Join-Path $repoRoot "docs\upstream-port-state.json") -Raw | ConvertFrom-Json

git -C $repoRoot fetch $Remote $Branch
if ($LASTEXITCODE -ne 0) {
	throw "Failed to fetch $Remote/$Branch."
}

$range = "$($state.lastCompletedJavaCommit)..refs/remotes/$Remote/$Branch"
git -C $repoRoot log --reverse --format="%H`t%cs`t%s" $range
if ($LASTEXITCODE -ne 0) {
	throw "Failed to list pending upstream commits for $range."
}

