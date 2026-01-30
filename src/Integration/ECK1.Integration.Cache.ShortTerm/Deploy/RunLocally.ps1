$ErrorActionPreference = 'Stop'

. ".github\scripts\common.ps1"

$baseDir = "src/Integration/ECK1.Integration.Cache.ShortTerm"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "cache-short-term"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "$imageName-release"

# 1. Ensure local registry
Start-LocalDockerRegistry

# 2 & 3. Build and push API image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 4. Check if Helm is installed
Ensure-Helm

# 5. Deploy using Helm
. ".github\scripts\prepare.global.vars.ps1"

# Ensure helm dependencies for the chart are present (copies library charts into charts/)
Write-Host "Updating Helm chart dependencies for $baseDir/Deploy"
helm dependency update "$baseDir/Deploy"

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

# helm template $baseDir\Deploy\$chartPath `
#     --namespace $env:AppServiceNamespace `
#     -f $baseDir\Deploy\values.local.yaml `
#     -f $baseDir\Deploy\values.secrets.yaml `
#     --debug

helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
    --namespace $env:AppServiceNamespace `
    --set environment=local `
    -f $baseDir\Deploy\values.local.yaml `
    -f $baseDir\Deploy\values.secrets.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "Deployment completed."

Write-Host "All Done."