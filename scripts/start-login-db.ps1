param(
	[string]$ContainerName = "aion-ls-mysql",
	[string]$RootPassword = "aion",
	[string]$Database = "aion_ls",
	[int]$HostPort = 3307
)

docker info *> $null
if ($LASTEXITCODE -ne 0) {
	throw "Docker is installed, but the Docker engine is not running. Start Docker Desktop, then run this script again."
}

$existing = docker ps -a --filter "name=^/$ContainerName$" --format "{{.Names}}"
if ($existing -eq $ContainerName) {
	docker start $ContainerName | Out-Null
} else {
	docker run `
		--name $ContainerName `
		-e MYSQL_ROOT_PASSWORD=$RootPassword `
		-e MYSQL_DATABASE=$Database `
		-p "${HostPort}:3306" `
		-d mysql:8.4 | Out-Null
}

Write-Host "Waiting for MySQL container $ContainerName on localhost:$HostPort..."
$deadline = (Get-Date).AddSeconds(90)
do {
	Start-Sleep -Seconds 2
	docker exec $ContainerName mysqladmin ping -uroot "-p$RootPassword" --silent 2>$null
	if ($LASTEXITCODE -eq 0) {
		Write-Host "MySQL is ready."
		Write-Host "Connection: Server=localhost;Port=$HostPort;Database=$Database;User ID=root;Password=$RootPassword"
		exit 0
	}
} while ((Get-Date) -lt $deadline)

throw "Timed out waiting for MySQL container $ContainerName."
