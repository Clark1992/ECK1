param(
    [string]$ImageName,
    [string]$ImageTag,
    [string]$Registry
)

$ErrorActionPreference = "Stop"

$fullImage = "$Registry/${ImageName}:$ImageTag"
$dockerfilePath = Join-Path $PSScriptRoot "Dockerfile"

Write-Host "Building DbUp image: $fullImage"

docker build -t $fullImage -f $dockerfilePath $PSScriptRoot
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker build failed"
    exit 1
}

Write-Host "Pushing DbUp image: $fullImage"

docker push $fullImage
if ($LASTEXITCODE -ne 0) {
    Write-Error "Docker push failed"
    exit 1
}
