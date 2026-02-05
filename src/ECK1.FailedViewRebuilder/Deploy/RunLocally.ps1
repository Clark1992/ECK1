$ErrorActionPreference = 'Stop'

. ".github\scripts\common.ps1"

$baseDir = "src/ECK1.FailedViewRebuilder"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "failed-view-rebuilder"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$serviceChartPath = "service"
$appReleaseName = "$imageName-release"

# 1. Ensure local registry
Start-LocalDockerRegistry

# 1.1 Ensure DbUp image
Ensure-DbUpImage

# 2 & 3. Build and push API image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 4. Check if Helm is installed
Ensure-Helm

# 5. Deploy using Helm
. ".github\scripts\prepare.global.vars.ps1"

Write-Host "Deploying app Helm release..."
helm upgrade --install $appReleaseName $baseDir\Deploy\$serviceChartPath `
    --namespace $env:AppServiceNamespace `
    --set environment=local `
    -f $baseDir\Deploy\service\values.local.yaml `
    -f $baseDir\Deploy\service\values.secrets.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "Deployment completed successfully."

Write-Host "All Done."
