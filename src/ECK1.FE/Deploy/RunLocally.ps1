$ErrorActionPreference = 'Stop'

. ".github\scripts\common.ps1"

$baseDir = "src/ECK1.FE"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "eck1-fe"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "$imageName-release"

# 1. Ensure local registry
Start-LocalDockerRegistry

# 2. Load global vars
. ".github\scripts\prepare.global.vars.ps1"

# 3. Build and push image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 4. Check if Helm is installed
Ensure-Helm

# 5. Deploy using Helm
helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
    --namespace $env:AppServiceNamespace `
    -f $baseDir\Deploy\values.local.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "All Done."
