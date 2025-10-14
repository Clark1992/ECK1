. "src\_SolutionItems\Deploy\Scripts\Common.ps1"

$ErrorActionPreference = "Stop"

Write-Host "🔹 Deploying Local infrastructure..."

Ensure-Helm

$env = "local"

# & ${PSScriptRoot}\Deploy.k8s.ingress.ps1 -Environment $env

& ${PSScriptRoot}\Deploy.k8s.strimzi.kafka.ps1 -Environment $env

& ${PSScriptRoot}\Deploy.k8s.kafka.ui.ps1 -Environment $env

& ${PSScriptRoot}\Deploy.k8s.apicurio.ps1 -Environment $env

if ($env -eq "local") {
    & ${PSScriptRoot}\Local.SetupSecrets.ps1
}