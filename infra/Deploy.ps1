param(
    [string]$Environment = "local"
)

. "src\_SolutionItems\Deploy\Scripts\Common.ps1"

$ErrorActionPreference = "Stop"

Write-Host "🔹 Deploying $Environment infrastructure..."

try {
    Ensure-Helm

    & ${PSScriptRoot}\Deploy.k8s.ingress.ps1 -Environment $Environment

    & ${PSScriptRoot}\Deploy.k8s.strimzi.kafka.ps1 -Environment $Environment

    & ${PSScriptRoot}\Deploy.k8s.kafka.ui.ps1 -Environment $Environment

    & ${PSScriptRoot}\Deploy.k8s.apicurio.ps1 -Environment $Environment
} catch {
    Write-Error "Error: $_"
    throw
}