$ErrorActionPreference = 'Stop'

. ".github\scripts\common.ps1"

$baseDir = "src/Integration/ECK1.Integration.Cache.LongTerm"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "cache-long-term"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "$imageName-release"

# 1. Ensure local registry
Start-LocalDockerRegistry

# 2. Load global vars (sets HELM_CHART_REGISTRY and other env vars from k8s ConfigMap)
. ".github\scripts\prepare.global.vars.ps1"

# 3. Ensure tools
Ensure-Tools

# 4. Build and push API image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 5. Check if Helm is installed
Ensure-Helm

# Ensure helm dependencies for the chart are present (copies library charts into charts/)
Resolve-HelmDependencies -ChartDir "$baseDir/Deploy"

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
