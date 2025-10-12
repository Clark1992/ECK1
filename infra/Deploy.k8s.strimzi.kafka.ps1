param(
    [string]$Namespace = "kafka",
    [string]$Environment = "local",
    [string]$OperatorVersion = "0.45.0",
    [string]$KafkaUserSecretName = "kafka-user"
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

# --- Wait for KafkaUser secret ---
Write-Host "Waiting for KafkaUser secret '$KafkaUserSecretName' to be created..."

$maxAttempts = 60
$attempt = 0
while ($attempt -lt $maxAttempts) {
    $secret = kubectl get secret $KafkaUserSecretName -n $KafkaNamespace --ignore-not-found
    if ($LASTEXITCODE -eq 0 -and $secret) {
        Write-Host "KafkaUser secret '$KafkaUserSecretName' found."
        break
    }
    Start-Sleep -Seconds 5
    $attempt++
    Write-Host "Still waiting... ($attempt/$maxAttempts)"
}

if (-not $secret) {
    throw "Timeout waiting for KafkaUser secret '$KafkaUserSecretName'."
    exit 1
}

# --- Extract credentials ---
Write-Host "Extracting KafkaUser credentials..."

$PasswordB64 = kubectl get secret $KafkaUserSecretName -n $KafkaNamespace -o jsonpath='{.data.password}'
$Password = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($PasswordB64))

$jaasConfigB64 = kubectl get secret $KafkaUserSecretName -n $KafkaNamespace -o jsonpath='{.data.sasl.jaas.config}'
$jaasConfig = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($jaasConfigB64))
# --- Set environment variables for subsequent scripts ---
Write-Host "Setting environment variables for Kafka credentials..."

$KafkaClusterName = "$Environment-cluster"
$env:KAFKA_USERNAME = $KafkaUsername
$env:KAFKA_PASSWORD = $Password
$env:KAFKA_BOOTSTRAP_WITH_NAMESPACE = "$KafkaClusterName-kafka-bootstrap.$KafkaNamespace.svc.cluster.local:9092"
$env:KAFKA_BOOTSTRAP = "$KafkaClusterName-kafka-bootstrap:9092"
$env:KAFKA_JAAS_CONFIG = $jaasConfig

Write-Host "Kafka credentials ready:"
Write-Host "  KAFKA_USERNAME=$env:KAFKA_USERNAME"
Write-Host "  KAFKA_PASSWORD=<hidden>"
Write-Host "  KAFKA_BOOTSTRAP=$env:KAFKA_BOOTSTRAP"
Write-Host "  KAFKA_BOOTSTRAP_WITH_NAMESPACE=$env:KAFKA_BOOTSTRAP_WITH_NAMESPACE"
Write-Host "  KAFKA_JAAS_CONFIG=<hidden>"
