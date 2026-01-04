. ".github\scripts\common.ps1"

$baseDir = "src/ECK1.QueriesApi"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "queries-api"
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

helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
    --namespace $env:AppServiceNamespace `
    -f $baseDir\Deploy\values.local.yaml `
    -f $baseDir\Deploy\values.secrets.yaml `
    --set ElasticSearchConfig__CaSecretName=$env:ElasticSearchConfig__CaSecretName `
    --set ElasticSearchConfig__ApiKeySecretName=$env:ElasticSearchConfig__ApiKeySecretName `
    --set env.ElasticSearchConfig__ClusterUrl=$env:ElasticSearchConfig__ClusterUrl

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "Deployment completed."

Write-Host "All Done."