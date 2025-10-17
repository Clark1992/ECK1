. "src\_SolutionItems\Deploy\Scripts\Common.ps1"

$baseDir = "src/ECK1.QueriesApi"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "queries-api"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "queries-api-release"
$namespace = "default"

# 1. Ensure local registry
Start-LocalDockerRegistry

# 2 & 3. Build and push API image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 4. Check if Helm is installed
Ensure-Helm

helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
    --namespace $namespace `
    -f $baseDir\Deploy\values.local.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "Deployment completed."

Write-Host "All Done."