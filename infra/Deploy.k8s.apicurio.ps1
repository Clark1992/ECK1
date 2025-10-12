param(
    [string]$Namespace = "apicurio",
    [string]$Environment = "local",
    [string]$KafkaNamespace = "kafka"
)

kubectl get ns $Namespace -o name 2>$null
if ($LASTEXITCODE -ne 0) {
    kubectl create namespace $Namespace
}

helm upgrade --install apicurio ./infra/k8s/charts/kafka/schema-registry/apicurio `
  --namespace $Namespace `
  --set kafka.bootstrapServers=$env:KAFKA_BOOTSTRAP_WITH_NAMESPACE `
  -f ./infra/k8s/charts/kafka/schema-registry/apicurio/values.${Environment}.yaml

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Apicurio Registry deployed successfully!"
} else {
    exit 1
}
