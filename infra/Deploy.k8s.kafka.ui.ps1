param(
    [string]$Namespace = "kafka",
    [string]$ReleaseName = "kafka-ui",
    [string]$Environment = "local"
)

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
helm upgrade --install $ReleaseName kafka-ui/kafka-ui `
    --namespace $Namespace `
    -f ./infra/k8s/charts/kafka/ui/values.${Environment}.yaml `
     --set yamlApplicationConfig.kafka.clusters[0].name=${Environment} `
    --set yamlApplicationConfig.kafka.clusters[0].bootstrapServers=$env:KAFKA_BOOTSTRAP `
    --set env.KAFKA_CLUSTERS_0_PROPERTIES_SASL_MECHANISM="SCRAM-SHA-512" `
    --set env.KAFKA_CLUSTERS_0_PROPERTIES_SECURITY_PROTOCOL="SASL_PLAINTEXT" `
    --set env.KAFKA_CLUSTERS_0_PROPERTIES_SASL_JAAS_CONFIG=$env.KAFKA_JAAS_CONFIG `
    --create-namespace


if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ === Kafka UI deployed successfully! ==="
} else {
    Write-Host "❌ Failed to apply Kafka UI!"
    exit 1
}
