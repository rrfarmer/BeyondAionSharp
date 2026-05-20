param(
	[switch]$StopDatabase,
	[string]$DatabaseContainerName = "aion-mixed-mysql"
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$composeFile = Join-Path $repoRoot "compose.mixed-csharp-login-chat.yml"

Push-Location $repoRoot
try {
	docker compose -f $composeFile down
} finally {
	Pop-Location
}

if ($StopDatabase) {
	$existing = docker ps -a --filter "name=^/$DatabaseContainerName$" --format "{{.Names}}"
	if ($existing -eq $DatabaseContainerName) {
		docker stop $DatabaseContainerName | Out-Null
		Write-Host "Stopped database container $DatabaseContainerName"
	}
}
