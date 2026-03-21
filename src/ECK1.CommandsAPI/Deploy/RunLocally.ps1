$ErrorActionPreference = 'Stop'

. ".github\scripts\common.ps1"

$baseDir = "src/ECK1.CommandsAPI"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "commands-api"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$serviceChartPath = "service"
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

Write-Host "Deploy manifests."

# Write-Host "helm upgrade --install ${releaseName}-elastic-mappings $baseDir/Deploy/index-mappings/$chartPath `
#     --namespace $env:AppServiceNamespace `
#     --set elasticsearch.clusterUrl=$env:ElasticSearchConfig__ClusterUrl `
#     --set elasticsearch.password=$env:ELASTICSEARCH_PASSWORD"

helm upgrade --install ${releaseName}-manifests $baseDir/Deploy/integration-manifests/$chartPath `
    --namespace $env:AppServiceNamespace `
    --set elasticsearch.clusterUrl=$env:ElasticSearchConfig__ClusterUrl `
    --set elasticsearch.password=$env:ELASTICSEARCH_PASSWORD

if ($LASTEXITCODE -ne 0) {
    throw
}

Write-Host "Deploy manifests: Successful."

Write-Host "Deploying app Helm release..."
helm upgrade --install $releaseName $baseDir\Deploy\$serviceChartPath `
    --namespace $env:AppServiceNamespace `
    --set environment=local `
    --set redis.host=$env:REDIS_HOST `
    --set redis.port=$env:REDIS_PORT `
    -f $baseDir\Deploy\service\values.local.yaml `
    -f $baseDir\Deploy\service\values.secrets.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "All Done."
