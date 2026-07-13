param(
	[switch]$Build,
	[switch]$Detached,
	[switch]$ResetSchema,
	[switch]$SkipDatabase
)

$ErrorActionPreference = "Stop"

$javaRepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\aion-server")
$composeFile = Join-Path $javaRepoRoot "compose.mixed-csharp-login-chat.yml"

if (-not $SkipDatabase) {
	$dbArgs = @()
	if ($ResetSchema) {
		$dbArgs += "-ResetSchema"
	}
	& (Join-Path $PSScriptRoot "start-mixed-mode-db.ps1") @dbArgs
}

Push-Location $javaRepoRoot
try {
	if ($Build) {
		docker compose -f $composeFile build game
	}

	if ($Detached) {
		docker compose -f $composeFile up -d game
	} else {
		docker compose -f $composeFile up game
	}
} finally {
	Pop-Location
}
