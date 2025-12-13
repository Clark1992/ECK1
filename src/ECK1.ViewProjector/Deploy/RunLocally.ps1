. ".github\scripts\common.ps1"
. "src\ECK1.ViewProjector\Deploy\local-mongo-ensure-stopped.ps1"

$baseDir = "src/ECK1.ViewProjector"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "view-projector"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "$imageName-release"

$mongoPort = 32017 # container port 27017 mapped to host port 32017

# 1. Ensure local registry
Start-LocalDockerRegistry

# 2 & 3. Build and push API image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 4. Check if Helm is installed
Ensure-Helm

# 5. Decide whether Helm should shutdown local mongo under docker before deploy to k8s
try {
    Ensure-LocalMongoStopped -mongoPort $mongoPort
    if ($LASTEXITCODE -ne 0) {
        throw
    }
} catch {
    Write-Host $_
    throw
}

Write-Host "Mongo port $mongoPort is not in use. Helm will deploy mongo inside k8s alongside the API."

# 6. Deploy using Helm

. ".github\scripts\prepare.global.vars.ps1"

helm upgrade --install $releaseName $baseDir\Deploy\service\$chartPath `
    --namespace $env:AppServiceNamespace `
    -f $baseDir\Deploy\service\values.local.yaml `
    -f $baseDir\Deploy\service\values.secrets.yaml

if ($LASTEXITCODE -ne 0) {
    throw
}

Write-Host "Service Deployment: Successful."

Write-Host "Run Mongo Migrations."
& ${PSScriptRoot}\RunMongoMigrations.ps1 -WorkingDir $baseDir
Write-Host "Run Mongo Migrations: Successful."

Write-Host "Deploy ES mappings."

# Write-Host "helm upgrade --install ${releaseName}-elastic-mappings $baseDir/Deploy/index-mappings/$chartPath `
#     --namespace $env:AppServiceNamespace `
#     --set elasticsearch.clusterUrl=$env:ElasticSearchConfig__ClusterUrl `
#     --set elasticsearch.password=$env:ELASTICSEARCH_PASSWORD"

helm upgrade --install ${releaseName}-elastic-mappings $baseDir/Deploy/integration-manifests/$chartPath `
    --namespace $env:AppServiceNamespace `
    --set elasticsearch.clusterUrl=$env:ElasticSearchConfig__ClusterUrl `
    --set elasticsearch.password=$env:ELASTICSEARCH_PASSWORD

if ($LASTEXITCODE -ne 0) {
    throw
}

Write-Host "Deploy ES mappings: Successful."

Write-Host "All Done."