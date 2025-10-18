param(
    [string]$Environment = "local"
)

. "src\_SolutionItems\Deploy\Scripts\Common.ps1"

$ErrorActionPreference = "Stop"

Write-Host "🔹 Deploying $Environment infrastructure..."

try {
    Ensure-Helm

    & ${PSScriptRoot}\Deploy.prepare.context.ps1 -Environment $Environment

    & ${PSScriptRoot}\Deploy.k8s.ingress.ps1 -Environment $Environment

    & ${PSScriptRoot}\Deploy.k8s.strimzi.kafka.ps1 -Environment $Environment

    & ${PSScriptRoot}\Deploy.k8s.kafka.ui.ps1 -Environment $Environment

    & ${PSScriptRoot}\Deploy.k8s.apicurio.ps1 -Environment $Environment

    & ${PSScriptRoot}\Deploy.k8s.kafka.secrets.ps1 `
        -BootstrapServers "$env:KAFKA_BOOTSTRAP_WITH_NAMESPACE" `
        -SchemaRegistryUrl "$env:SCHEMAREGISTRYURL_INTERNAL" `
        -User "$env:KAFKA_USERNAME" `
        -Secret "$env:KAFKA_PASSWORD"
} catch {
    Write-Error "Error: $_"
    throw
}