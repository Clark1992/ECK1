. "src\_SolutionItems\Deploy\Scripts\Common.ps1"
. "src\_SolutionItems\Deploy\Scripts\RunDbUpMigrations.ps1"

$baseDir = "src/ECK1.CommandsAPI"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "commands-api"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "commands-api-release"
$namespace = "default"

# 1. Ensure local registry
Start-LocalDockerRegistry

# 2 & 3. Build and push API image to local registry
Build-DockerImage -imageNameWithTag $imageNameWithTag -dockerfilePath $dockerfilePath

# 4. Check if Helm is installed
Ensure-Helm

# 5. Deploy using Helm
Write-Host "Deploying Helm chart..."
helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
    --namespace $namespace `
    -f $baseDir\Deploy\values.local.yaml `
    -f $baseDir\Deploy\values.secrets.yaml

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

$cString = Get-YamlValue -YamlPath "$baseDir/Deploy/values.local.yaml" -PropPath "env.ConnectionStrings__DefaultConnection"

Run-DbUp -ScriptsPath "$baseDir/Migrations" -ConnectionString $cString

Write-Host "All Done."
