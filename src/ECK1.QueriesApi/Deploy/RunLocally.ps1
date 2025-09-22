$registryName = "local-registry"
$registryPort = 5000
$dockerfilePath = ".."
$imageName = "queries-api"
$imageTag = "dev"
$fullImageName = "localhost:${registryPort}/${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "queries-api-release"
$namespace = "default"

$mongoPort = 32017 # container port 27017 mapped to host port 32017

$helmVersion = "v3.12.0"
$helmZipName = "helm-$helmVersion-windows-amd64.zip"
$zipPath = "$env:TEMP\$helmZipName"
$extractPath = "$env:TEMP\helm-$helmVersion"
$helmExe = "$extractPath\windows-amd64\helm.exe"

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

# 1. Ensure local registry
$registryRunning = docker ps --filter "name=$registryName" --filter "status=running" --quiet
if ([string]::IsNullOrWhiteSpace($registryRunning)) {
    Write-Host "Starting local registry..."
    docker run -d -p "${registryPort}:5000" --restart=always --name $registryName registry:2
} else {
    Write-Host "Local registry already running."
}

# 2. Build and push API image to local registry
Write-Host "Building API image $fullImageName..."
docker build -t $fullImageName $dockerfilePath
if ($LASTEXITCODE -ne 0) { Write-Error "Docker build failed"; exit 1 }

# 3. Push image to local registry
Write-Host "Pushing API image to local registry..."
docker push $fullImageName
if ($LASTEXITCODE -ne 0) { Write-Error "Docker push failed"; exit 1 }

# 4. Check if Helm is installed
if (-not (Get-Command helm -ErrorAction SilentlyContinue)) {
    Write-Host "Helm not found in system. Using temporary path."

    # 4.1. Download zip if not already downloaded
    if (-not (Test-Path $zipPath)) {
        Write-Host "Downloading Helm ZIP..."
        $helmUrl = "https://get.helm.sh/$helmZipName"
        Invoke-WebRequest -Uri $helmUrl -OutFile $zipPath
    }
    else {
        Write-Host "Helm ZIP already downloaded: $zipPath"
    }

    # 4.2. Extract if not already extracted
    if (-not (Test-Path $helmExe)) {
        Write-Host "Extracting Helm..."
        Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
    }
    else {
        Write-Host "Helm already extracted: $helmExe"
    }

    # 4.3. Add to current session PATH
    $env:PATH = "$extractPath\windows-amd64;" + $env:PATH
    Write-Host "Helm added to temporary PATH: $helmExe"
}
else {
    Write-Host "Helm found in system: $(Get-Command helm)."
}

# 5. Decide whether Helm should shutdown local mongo under docker before deploy to k8s
$mongoInUse = Test-PortInUse -port $mongoPort
if ($mongoInUse) {
    Write-Host "Mongo port $mongoPort is in use. Checking if the listener is a Docker container..."

    # Check docker for any running container that exposes port 32017
    $dockerCandidates = @()
    try {
        $dockerCandidates = docker ps --format "{{.ID}}|{{.Names}}|{{.Ports}}|{{.Image}}" 2>$null
    } catch {
        $dockerCandidates = @()
    }

    $dockerMongoLine = $null
    foreach ($line in $dockerCandidates) {
        if ($line -and $line -match $mongoPort) {
            $dockerMongoLine = $line
            break
        }
    }

    if ($dockerMongoLine) {
        $parts = $dockerMongoLine -split '\|'
        $cid = $parts[0]
        $cname = $parts[1]
        $cports = $parts[2]
        $cimage = $parts[3]
        Write-Host "Detected Docker container exposing port ${mongoPort}: $cname ($cid) image:$cimage ports:$cports"
        Write-Host "Stopping and removing Docker container so Helm can deploy Mongo in k8s..."
        docker stop $cid | Out-Null

        # Retry check: give the OS / Docker a moment to release the port.
        # If the port is still in use, retry up to 5 times with 2s delay.
        $maxRetries = 5
        $waitSeconds = 2
        $attempt = 0
        $mongoInUse = Test-PortInUse -port $mongoPort
        while ($mongoInUse -and ($attempt -lt $maxRetries)) {
            $attempt++
            Write-Host "Port $mongoPort still in use. Retry $attempt of $maxRetries after $waitSeconds seconds..."
            Start-Sleep -Seconds $waitSeconds

            # Try to find any lingering docker container mapping the port and remove it again
            try {
                $lingering = docker ps -a --filter "publish=$mongoPort" --format "{{.ID}}" 2>$null
                if ($lingering) {
                    foreach ($lid in $lingering) {
                        Write-Host "Found lingering container $lid; stopping and removing..."
                        docker stop $lid | Out-Null
                        docker rm $lid | Out-Null
                    }
                }
            } catch {
                # ignore docker errors on cleanup
            }

            $mongoInUse = Test-PortInUse -port $mongoPort
        }

        if ($mongoInUse) {
            Write-Host "Port $mongoPort is still in use after $maxRetries retries. Failing deploy."
            exit 1
        }
    } else {
        # check maybe it is k8s mongo
        $isMongoAlreadyOnK8s = kubectl get pods -A | findstr mongo | findstr Running

        if (-not $isMongoAlreadyOnK8s) {
            Write-Host "Couldn't find a Mongo container to shutdown. Failing deploy."
            exit 1
        } else {
            Write-Host "Mongo already running on k8s. Proceeding with deploy."
        }
    }
} 

Write-Host "Mongo port $mongoPort is not in use. Helm will deploy mongo inside k8s alongside the API."
helm upgrade --install $releaseName $chartPath `
    --namespace $namespace `
    -f values.local.yaml `
    -f values.secret.yaml

Write-Host "Deployment completed."

& .\RunMongoMigrations.ps1 -WorkingDir ".."

Write-Host "All Done."