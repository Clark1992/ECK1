# RunLocally.OnlyMongo.ps1
# Checks if local Mongo port (32017) is in use. If not, builds and runs a mongo container from Dockerfile.RunMongo.

$mongoPort = 32017 #on host
$mongoContainerName = "queriesapi-mongo-local"
# Use the public mongo image (change if you need a custom build)
$mongoImage = "mongo:6.0"

function Test-PortInUse {
    param([int]$port)
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $async = $tcp.BeginConnect('127.0.0.1', $port, $null, $null)
        $wait = $async.AsyncWaitHandle.WaitOne(500)
        if ($wait -and $tcp.Connected) { $tcp.Close(); return $true }
        return $false
    } catch {
        return $false
    }
}

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