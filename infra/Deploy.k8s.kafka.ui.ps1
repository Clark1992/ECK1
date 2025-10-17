param(
    [string]$Namespace = "kafka",
    [string]$ReleaseName = "kafka-ui",
    [string]$Environment = "local",
    [string]$KafkaUiVersion = "0.7.6"
)

$ErrorActionPreference = "Stop"

Write-Host "=== Setting up Kafka UI in namespace '$Namespace' ==="

# Create namespace if it does not exist
if (-not (kubectl get ns $Namespace -o name 2>$null)) {
    Write-Host "Namespace '$Namespace' not found, creating..."
    kubectl create namespace $Namespace
} else {
    Write-Host "Namespace '$Namespace' already exists."
}

# Add Helm repo
Write-Host "Adding Helm repo for Provectus Kafka UI..."
helm repo add kafka-ui https://provectus.github.io/kafka-ui-charts
helm repo update

# Deploy via Helm
Write-Host "Deploying Kafka UI Helm release '$ReleaseName'..."

$jaasConfigKey = "yamlApplicationConfig.kafka.clusters[0].properties.sasl\.jaas\.config"

$jaasConfig = $env:KAFKA_JAAS_CONFIG.Replace('"', '\"')

helm upgrade --install $ReleaseName kafka-ui/kafka-ui `
    --namespace $Namespace `
    -f ./infra/k8s/charts/kafka/ui/values.${Environment}.yaml `
    --set yamlApplicationConfig.kafka.clusters[0].name=${Environment} `
    --set yamlApplicationConfig.kafka.clusters[0].bootstrapServers=$env:KAFKA_BOOTSTRAP `
    --set yamlApplicationConfig.kafka.clusters[0].properties.security.protocol="SASL_PLAINTEXT" `
    --set yamlApplicationConfig.kafka.clusters[0].properties.sasl.mechanism="SCRAM-SHA-512" `
    --set-string "$jaasConfigKey=$jaasConfig" `
    --create-namespace `
    --version $KafkaUiVersion


if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ === Kafka UI deployed successfully! ==="
} else {
    Write-Host "❌ Failed to apply Kafka UI!"
    throw
}
