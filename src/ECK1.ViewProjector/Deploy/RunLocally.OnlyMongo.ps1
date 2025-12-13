# RunLocally.OnlyMongo.ps1
# Checks if local Mongo port (32017) is in use. If not, builds and runs a mongo container from Dockerfile.RunMongo.

. ".github\scripts\common.ps1"

$mongoPort = 32017 #on host
$mongoContainerName = "mongo-local"
# Use the public mongo image (change if you need a custom build)
$mongoImage = "mongo:6.0"

if (Test-PortInUse -port $mongoPort) {
    Write-Host "Mongo port $mongoPort appears in use. Doing nothing."
    exit 0
}

Write-Host "Mongo port $mongoPort is free. Pulling and starting local mongo container using image: $mongoImage"

$existing = docker ps -a --filter "name=$mongoContainerName" --quiet
if ([string]::IsNullOrWhiteSpace($existing)) {
    docker run -d -p "${mongoPort}:27017" --name $mongoContainerName $mongoImage
} else {
    $running = docker ps --filter "name=$mongoContainerName" --filter "status=running" --quiet
    if ([string]::IsNullOrWhiteSpace($running)) {
        docker start $mongoContainerName
    } else {
        Write-Host "Mongo container already running"
    }
}

Write-Host "Mongo ready."

& .\RunMongoMigrations.ps1 -WorkingDir ".."

Write-Host "All Done."