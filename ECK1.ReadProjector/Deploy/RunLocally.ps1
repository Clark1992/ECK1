$registryName = "local-registry"
$registryPort = 5000
$dockerfilePath = ".."
$imageName = "read-projector"
$imageTag = "dev"
$fullImageName = "localhost:${registryPort}/${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "read-projector-release"
$namespace = "default"

$mongoPort = 32017 # container port 27017 mapped to host port 32017

$helmVersion = "v3.12.0"
$helmZipName = "helm-$helmVersion-windows-amd64.zip"
$zipPath = "$env:TEMP\$helmZipName"
$extractPath = "$env:TEMP\helm-$helmVersion"
$helmExe = "$extractPath\windows-amd64\helm.exe"

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

helm upgrade --install $releaseName $chartPath `
    --namespace $namespace `
    -f values.local.yaml `
    -f values.secret.yaml

Write-Host "Deployment completed."
Write-Host "All Done."