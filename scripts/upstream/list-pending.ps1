param(
	[string]$JavaRepository,
	[string]$Remote = "upstream",
	[string]$Branch = "4.8"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$javaRoot = if ($JavaRepository) {
	Resolve-Path $JavaRepository
} else {
	Resolve-Path (Join-Path $repoRoot "..\aion-server")
}
$state = Get-Content (Join-Path $repoRoot "docs\upstream-port-state.json") -Raw | ConvertFrom-Json

git -C $javaRoot fetch $Remote $Branch
if ($LASTEXITCODE -ne 0) {
	throw "Failed to fetch $Remote/$Branch in Java repository $javaRoot."
}

$range = "$($state.lastCompletedJavaCommit)..refs/remotes/$Remote/$Branch"
git -C $javaRoot log --reverse --format="%H`t%cs`t%s" $range
if ($LASTEXITCODE -ne 0) {
	throw "Failed to list pending upstream commits for $range."
}
