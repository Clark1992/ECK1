. ".github\scripts\common.ps1"

$baseDir = "tests/ECK1.TestPlatform"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "test-platform"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "$imageName-release"

# 1. Ensure local registry
Start-LocalDockerRegistry

# 2. Build and push API image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 3. Check if Helm is installed
Ensure-Helm

# 4. Deploy using Helm

. ".github\scripts\prepare.global.vars.ps1"

Write-Host "Deploying Helm chart..."
helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
    --namespace $env:AppServiceNamespace `
    -f $baseDir\Deploy\values.local.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "All Done."
