$ErrorActionPreference = 'Stop'

. ".github\scripts\common.ps1"

$baseDir = "src/Integration/ECK1.Integration.Proxy"
$dockerfilePath = "$baseDir/Dockerfile"
$imageName = "integration-proxy"
$imageTag = "dev"
$imageNameWithTag = "${imageName}:${imageTag}"
$chartPath = "."
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

Ensure-Gomplate

try {
    # Ensure helm dependencies for the chart are present (copies library charts into charts/)
    Resolve-HelmDependencies -ChartDir "$baseDir/Deploy"

    gomplate -f $baseDir\Deploy\values.plugins.yaml -o $baseDir\Deploy\values.plugins.rendered.yaml

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Helm deployment failed."
        throw
    }

    # helm template $baseDir\Deploy\$chartPath `
    #     --namespace $env:AppServiceNamespace `
    #     -f $baseDir\Deploy\values.local.yaml `
    #     -f $baseDir\Deploy\values.plugins.rendered.yaml `
    #     -f $baseDir\Deploy\values.secrets.yaml `
    #     --debug

    helm upgrade --install $releaseName $baseDir\Deploy\$chartPath `
        --namespace $env:AppServiceNamespace `
        --set environment=local `
        -f $baseDir\Deploy\values.local.yaml `
        -f $baseDir\Deploy\values.plugins.rendered.yaml `
        -f $baseDir\Deploy\values.secrets.yaml

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Helm deployment failed."
        throw
    }
}
finally {
    Remove-Item $baseDir\Deploy\values.plugins.rendered.yaml -Force -ErrorAction Ignore
}

Write-Host "Deployment completed."

Write-Host "All Done."