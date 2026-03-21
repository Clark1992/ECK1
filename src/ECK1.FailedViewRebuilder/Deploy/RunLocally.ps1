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

# 2. Load global vars (sets HELM_CHART_REGISTRY and other env vars from k8s ConfigMap)
. ".github\scripts\prepare.global.vars.ps1"

# 3. Ensure tools (DbUp image + config-watcher chart)
Ensure-Tools

# 4. Build and push API image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 5. Check if Helm is installed
Ensure-Helm

# Ensure helm dependencies for the chart are present (copies library charts into charts/)
Resolve-HelmDependencies -ChartDir "$baseDir/Deploy/$serviceChartPath"

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
