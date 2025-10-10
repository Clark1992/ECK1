param(
    [string]$Namespace = "kafka",
    [string]$Environment = "local",
    [string]$OperatorVersion = "0.45.0"
)

Write-Host "🔹 Deploying Strimzi Kafka Operator to namespace '$Namespace'..."

# 1️⃣ namespace
if (-not (kubectl get ns $Namespace -o name 2>$null)) {
    Write-Host "Namespace '$Namespace' not found, creating..."
    kubectl create namespace $Namespace
} else {
    Write-Host "Namespace '$Namespace' already exists."
}

# 2️⃣ Helm repo
helm repo add strimzi https://strimzi.io/charts/
helm repo update

# 3️⃣ Operator
helm upgrade --install strimzi strimzi/strimzi-kafka-operator `
  --namespace $Namespace `
  --version $OperatorVersion

if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Helm deployment failed!"
    exit 1
}

Write-Host "✅ Strimzi Kafka Operator deployed successfully!"

# 4️⃣ Apply Kafka cluster

Write-host "!!!!!!!!!!!!!!!!!!!!  REVIEW THIS  !!!!!!!!!!!!!!!!!!!!!"

if ($Environment -eq 'local') {
  helm upgrade --install kafka ./infra/k8s/charts/kafka/cluster `
    --namespace $Namespace `
    --create-namespace `
    -f ./infra/k8s/charts/kafka/cluster/values.$Environment.yaml `
    -f ./infra/k8s/charts/kafka/cluster/values.secrets.yaml
} else {
  helm upgrade --install kafka ./infra/k8s/charts/kafka/cluster `
    --namespace $Namespace `
    --create-namespace `
    -f ./infra/k8s/charts/kafka/cluster/values.$Environment.yaml
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Kafka cluster applied successfully!"
} else {
    Write-Host "❌ Failed to apply Kafka cluster!"
    exit 1
}
