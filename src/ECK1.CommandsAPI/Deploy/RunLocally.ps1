$ErrorActionPreference = 'Stop'

. ".github\scripts\common.ps1"

$baseDir = "src/ECK1.CommandsAPI"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "commands-api"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$sqlChartPath = "sql"
$serviceChartPath = "service"
$sqlReleaseName = "$imageName-sql-release"
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

Write-Host "Deploying SQL Server Helm release..."
helm upgrade --install $sqlReleaseName $baseDir\Deploy\$sqlChartPath `
    --namespace $env:AppServiceNamespace `
    --set environment=local `
    --set sqlserver.otelCollector.image.repository="$env:OTEL_COLLECTOR_IMAGE_REPOSITORY" `
    --set sqlserver.otelCollector.image.tag="$env:OTEL_COLLECTOR_IMAGE_TAG" `
    --set sqlserver.otelCollector.otlpEndpoint="$env:OTEL_EXPORTER_OTLP_ENDPOINT" `
    -f $baseDir\Deploy\sql\values.local.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

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

Write-Host "All Done."
