param(
	[string]$ContainerName = "aion-mixed-mysql",
	[string]$RootPassword = "Farmer598!",
	[int]$HostPort = 3306,
	[switch]$ResetSchema
)

$ErrorActionPreference = "Stop"

docker info *> $null
if ($LASTEXITCODE -ne 0) {
	throw "Docker is installed, but the Docker engine is not running. Start Docker Desktop, then run this script again."
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$schemas = @(
	@{ Database = "aion_ls"; File = Join-Path $repoRoot "login-server\sql\aion_ls.sql"; ProbeTable = "account_data" },
	@{ Database = "aion_gs"; File = Join-Path $repoRoot "game-server\sql\aion_gs.sql"; ProbeTable = "abyss_rank" },
	@{ Database = "aion_cs"; File = Join-Path $repoRoot "chat-server\sql\aion_cs.sql"; ProbeTable = "chatlog" }
)

$created = $false
$existing = docker ps -a --filter "name=^/$ContainerName$" --format "{{.Names}}"
if ($existing -eq $ContainerName) {
	docker start $ContainerName | Out-Null
} else {
	$created = $true
	docker run `
		--name $ContainerName `
		-e MYSQL_ROOT_PASSWORD=$RootPassword `
		-p "${HostPort}:3306" `
		-d mysql:8.4 | Out-Null
}

Write-Host "Waiting for MySQL container $ContainerName on localhost:$HostPort..."
$deadline = (Get-Date).AddSeconds(90)
do {
	Start-Sleep -Seconds 2
	docker exec -e MYSQL_PWD=$RootPassword $ContainerName mysqladmin ping -uroot --silent 2>$null
	if ($LASTEXITCODE -eq 0) {
		Write-Host "MySQL is ready."
		break
	}
} while ((Get-Date) -lt $deadline)

if ($LASTEXITCODE -ne 0) {
	throw "Timed out waiting for MySQL container $ContainerName."
}

foreach ($schema in $schemas) {
	$database = $schema.Database
	$sqlFile = Resolve-Path $schema.File
	docker exec -e MYSQL_PWD=$RootPassword $ContainerName mysql -uroot -e "CREATE DATABASE IF NOT EXISTS ``$database`` CHARACTER SET utf8mb4;" | Out-Null

	$probeTable = $schema.ProbeTable
	$existingTable = docker exec -e MYSQL_PWD=$RootPassword $ContainerName mysql -N -uroot $database -e "SHOW TABLES LIKE '$probeTable';"
	$needsSchema = [string]::IsNullOrWhiteSpace($existingTable)

	if ($created -or $ResetSchema -or $needsSchema) {
		$containerSqlPath = "/tmp/$database.sql"
		docker cp $sqlFile "${ContainerName}:$containerSqlPath" | Out-Null
		docker exec -e MYSQL_PWD=$RootPassword $ContainerName sh -c "mysql -uroot $database < $containerSqlPath"
		Write-Host "Initialized $database from $sqlFile"
	}
}

docker exec -e MYSQL_PWD=$RootPassword $ContainerName mysql -uroot aion_ls -e "REPLACE INTO gameservers (id, mask, password) VALUES (1, '*', '1234');" | Out-Null

Write-Host "Seeded login DB gameservers row: id=1, mask=*, password=1234"
Write-Host "Connection: Server=localhost;Port=$HostPort;User ID=root;Password=$RootPassword"
Write-Host "Databases: aion_ls, aion_gs, aion_cs"
