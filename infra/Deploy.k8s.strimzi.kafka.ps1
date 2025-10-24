# param(
#     [string]$Namespace = "kafka",
#     [string]$Environment = "local",
#     [string]$OperatorVersion = "0.45.0",
#     [string]$KafkaUserSecretName = "kafka-user",
#     [string]$K8sHost = "localhost"
# )

# $ErrorActionPreference = "Stop"

# Write-Host "🔹 Deploying Strimzi Kafka Operator to namespace '$Namespace'..."

# # 1️⃣ namespace
# # if (-not (kubectl get ns $Namespace -o name 2>$null)) {
# #     Write-Host "Namespace '$Namespace' not found, creating..."
# #     kubectl create namespace $Namespace
# # } else {
# #     Write-Host "Namespace '$Namespace' already exists."
# # }

# # 2️⃣ Helm repo
# helm repo add strimzi https://strimzi.io/charts/
# helm repo update

# # 3️⃣ Operator
# helm upgrade --install strimzi strimzi/strimzi-kafka-operator `
#   --namespace $Namespace `
#   --create-namespace
#   --version $OperatorVersion

# if ($LASTEXITCODE -ne 0) {
#     Write-Host "❌ Helm deployment failed!"
#     throw
# }

# Write-Host "✅ Strimzi Kafka Operator deployed successfully!"

# # 4️⃣ Apply Kafka cluster

# Write-host "!!!!!!!!!!!!!!!!!!!!  REVIEW THIS  !!!!!!!!!!!!!!!!!!!!!"

# if ($Environment -eq 'local') {
#   helm upgrade --install kafka ./infra/k8s/charts/kafka/cluster `
#     --namespace $Namespace `
#     --create-namespace `
#     -f ./infra/k8s/charts/kafka/cluster/values.yaml `
#     -f ./infra/k8s/charts/kafka/cluster/values.$Environment.yaml `
#     -f ./infra/k8s/charts/kafka/cluster/values.secrets.yaml
# } else {
#   helm upgrade --install kafka ./infra/k8s/charts/kafka/cluster `
#     --namespace $Namespace `
#     --create-namespace `
#     -f ./infra/k8s/charts/kafka/cluster/values.yaml `
#     -f ./infra/k8s/charts/kafka/cluster/values.$Environment.yaml
# }

# if ($LASTEXITCODE -eq 0) {
#     Write-Host "✅ Kafka cluster applied successfully!"
# } else {
#     Write-Host "❌ Failed to apply Kafka cluster!"
#     throw
# }

# # --- Wait for KafkaUser secret ---
# Write-Host "Waiting for KafkaUser secret '$KafkaUserSecretName' to be created..."

# kubectl wait --for=condition=Ready kafkauser/kafka-user -n $Namespace --timeout=600s

# $maxAttempts = 60
# $attempt = 0
# while ($attempt -lt $maxAttempts) {
#     $secret = kubectl get secret $KafkaUserSecretName -n $Namespace --ignore-not-found
#     if ($LASTEXITCODE -eq 0 -and $secret) {
#         Write-Host "KafkaUser secret '$KafkaUserSecretName' found."
#         break
#     }
#     Start-Sleep -Seconds 5
#     $attempt++
#     Write-Host "Still waiting... ($attempt/$maxAttempts)"
# }

# if (-not $secret) {
#     throw "Timeout waiting for KafkaUser secret '$KafkaUserSecretName'."
#     throw
# }

# # --- Extract credentials ---
# Write-Host "Extracting KafkaUser credentials..."

# $PasswordB64 = kubectl get secret $KafkaUserSecretName -n $Namespace -o jsonpath='{.data.password}'
# $Password = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($PasswordB64))

# $jaasConfigB64 = kubectl get secret $KafkaUserSecretName -n $Namespace -o jsonpath="{.data['sasl\.jaas\.config']}"
# $jaasConfig = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($jaasConfigB64))

# Write-Host "Extracted"

# # --- Set environment variables for subsequent scripts ---
# Write-Host "Setting environment variables for Kafka credentials..."

# $KafkaClusterName = "$Environment-cluster"
# $env:KAFKA_CLUSTER = $KafkaClusterName
# $env:KAFKA_USERNAME = $KafkaUserSecretName
# $env:KAFKA_PASSWORD = $Password
# $env:KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE = "$KafkaClusterName-kafka-bootstrap.$Namespace.svc.cluster.local:9093"
# $env:KAFKA_BOOTSTRAP_WITH_NAMESPACE = "$KafkaClusterName-kafka-bootstrap.$Namespace.svc.cluster.local:9092"
# $env:KAFKA_JAAS_CONFIG = $jaasConfig

# $externalPort = Get-YamlValue -YamlPath "./infra/k8s/charts/kafka/cluster/values.$Environment.yaml" -PropPath "fixedPorts.bootstrapPort"
# $env:KAFKA_EXTERNAL_TLS_BOOTSTRAP = "${K8sHost}:${externalPort}"

# Write-Host "Kafka credentials ready:"
# Write-Host "  KAFKA_CLUSTER=$env:KAFKA_CLUSTER"
# Write-Host "  KAFKA_USERNAME=$env:KAFKA_USERNAME"
# Write-Host "  KAFKA_PASSWORD=<hidden>"
# Write-Host "  KAFKA_BOOTSTRAP_WITH_NAMESPACE=$env:KAFKA_BOOTSTRAP_WITH_NAMESPACE"
# Write-Host "  KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE=$env:KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE"
# Write-Host "  KAFKA_EXTERNAL_TLS_BOOTSTRAP=$env:KAFKA_EXTERNAL_TLS_BOOTSTRAP"
# Write-Host "  KAFKA_JAAS_CONFIG=<hidden>"

# Write-Host "✅ === Kafka cluster deployed successfully! ==="

# # =============================        TOPICS      ==============================
# Write-Host "Deploying topics..."

# helm upgrade --install kafka-topics ./infra/k8s/charts/kafka/topic `
#     --namespace $Namespace `
#     --create-namespace `
#     -f ./infra/k8s/charts/kafka/topic/values.$Environment.yaml

# Write-Host "✅ === Kafka Topics deployed successfully! ==="