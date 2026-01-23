. ".github\scripts\common.ps1"
. ".github\scripts\run-dbup-migrations.ps1"

$baseDir = "src/ECK1.FailedViewRebuilder"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "failed-view-rebuilder"
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

Write-Host "Deploying Helm chart..."
helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
    --namespace $env:AppServiceNamespace `
    --set environment=local `
    -f $baseDir\Deploy\values.local.yaml `
    -f $baseDir\Deploy\values.secrets.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "Deployment completed successfully."

$cString = Get-YamlValue -YamlPath "$baseDir/Deploy/values.local.yaml" -PropPath "env.ConnectionStrings__DefaultConnection"

Run-DbUp -ScriptsPath "$baseDir/Migrations" -ConnectionString $cString

Write-Host "All Done."
