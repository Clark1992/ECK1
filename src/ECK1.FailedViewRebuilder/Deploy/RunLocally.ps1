$ErrorActionPreference = 'Stop'

. ".github\scripts\common.ps1"

$baseDir = "src/ECK1.FailedViewRebuilder"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "failed-view-rebuilder"
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

$serviceConnectionString = "Server=$env:SQLSERVER_FAILED_VIEW_REBUILDER_HOST,$env:SQLSERVER_PORT;Database=$env:SQLSERVER_FAILED_VIEW_REBUILDER_DB;User Id=$env:SQLSERVER_FAILED_VIEW_REBUILDER_APP_USER;Password=$env:SQLSERVER_FAILED_VIEW_REBUILDER_APP_PASSWORD;TrustServerCertificate=True;Encrypt=False"

Write-Host "Deploying SQL Server Helm release..."
helm upgrade --install $sqlReleaseName $baseDir\Deploy\$sqlChartPath `
    --namespace $env:AppServiceNamespace `
    --set sqlserver.serviceName="$env:SQLSERVER_FAILED_VIEW_REBUILDER_SERVICE_NAME" `
    --set env.ConnectionStrings__DefaultConnection="$serviceConnectionString" `
    -f $baseDir\Deploy\sql\values.local.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "Deploying app Helm release..."
helm upgrade --install $appReleaseName $baseDir\Deploy\$serviceChartPath `
    --namespace $env:AppServiceNamespace `
    --set env.ConnectionStrings__DefaultConnection="$serviceConnectionString" `
    -f $baseDir\Deploy\service\values.local.yaml `
    -f $baseDir\Deploy\service\values.secrets.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "Deployment completed successfully."

Write-Host "All Done."
