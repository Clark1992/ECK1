param(
    [string]$ImageName = "alpine-toolbox",
    [string]$ImageTag = "dev",
    [string]$Registry = "localhost:5000"
)

$ErrorActionPreference = "Stop"

$fullImage = "$Registry/${ImageName}:$ImageTag"
$dockerfilePath = Join-Path $PSScriptRoot "Dockerfile"

Write-Host "Building alpine-toolbox image: $fullImage"

docker build -t $fullImage -f $dockerfilePath $PSScriptRoot
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker build failed"
    exit 1
}

Write-Host "Pushing alpine-toolbox image: $fullImage"

docker push $fullImage
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker push failed"
    exit 1
}
