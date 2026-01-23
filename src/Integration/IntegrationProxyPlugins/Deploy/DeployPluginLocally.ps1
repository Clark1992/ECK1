param(
    [Parameter(Mandatory=$true)]
    [string]$pluginProj,

    [Parameter(Mandatory=$true)]
    [string]$plugin
)

$PSDefaultParameterValues['*:ErrorAction']='Stop'

. ".github\scripts\common.ps1"

$pluginLower = $plugin.ToLower()
$baseDir = "src/Integration/IntegrationProxyPlugins"
$dockerfilePath = "$baseDir/Deploy/Dockerfile"
$imageName = "plugin-$pluginLower"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
$releaseName = "$imageName-release"

# 1. Ensure local registry
Start-LocalDockerRegistry

# 2 & 3. Build and push API image to local registry
Build-DockerImage `
 -imageNameWithTag $imageNameWithTag `
 -dockerfilePath $dockerfilePath `
 -BuildArgs @{ 
    PLUGIN_PROJ = $pluginProj 
    PLUGIN = $plugin
 }

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

# 4. Check if Helm is installed
Ensure-Helm

# 5. Deploy using Helm

. ".github\scripts\prepare.global.vars.ps1"

helm uninstall $releaseName --namespace $env:AppServiceNamespace

helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
    --namespace $env:AppServiceNamespace `
    --set environment=local `
    -f $baseDir\Deploy\values.local.yaml `
    --set plugin.name=$pluginLower

if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm deployment failed."
    throw
}

Write-Host "Deployment completed."

Write-Host "All Done."