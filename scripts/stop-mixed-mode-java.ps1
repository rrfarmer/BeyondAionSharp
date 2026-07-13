param(
	[switch]$StopDatabase,
	[string]$DatabaseContainerName = "aion-mixed-mysql"
)

$ErrorActionPreference = "Stop"

$javaRepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..\aion-server")
$composeFile = Join-Path $javaRepoRoot "compose.mixed-csharp-login.yml"

Push-Location $javaRepoRoot
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
