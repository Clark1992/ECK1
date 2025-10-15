param(
    [string]$Namespace = "apicurio",
    [string]$Environment = "local",
    [string]$KafkaNamespace = "kafka",
    [string]$KafkaUserSecret = "kafka-user",
    [string]$KafkaClusterSecretPattern = "{0}-cluster-ca-cert"
)

$ErrorActionPreference = "Stop"

kubectl get ns $Namespace -o name 2>$null
if ($LASTEXITCODE -ne 0) {
    kubectl create namespace $Namespace
}

Write-Host "Copying secret from $KafkaNamespace to $Namespace namespace"
# $secret = kubectl get secret $KafkaUserSecret -n $KafkaNamespace -o yaml > .github\scripts\test2.yaml

$secret = kubectl get secret $KafkaUserSecret -n $KafkaNamespace -o json
$secretObject = $secret | ConvertFrom-Json
$secretObject.metadata.PSObject.Properties.Remove('ownerReferences')
$secretObject.metadata.PSObject.Properties.Remove('namespace')
$secretObject.metadata.PSObject.Properties.Remove('resourceVersion')
$secretObject.metadata.PSObject.Properties.Remove('uid')
$secretObject.metadata.PSObject.Properties.Remove('creationTimestamp')
$secretYaml = $secretObject | ConvertTo-Yaml
$secretYaml | kubectl apply -n apicurio -f -

$KafkaClusterSecret = [string]::Format($KafkaClusterSecretPattern, $env:KAFKA_CLUSTER)

$secret = kubectl get secret $KafkaClusterSecret -n $KafkaNamespace -o json
$secretObject = $secret | ConvertFrom-Json
$secretObject.metadata.PSObject.Properties.Remove('ownerReferences')
$secretObject.metadata.PSObject.Properties.Remove('namespace')
$secretObject.metadata.PSObject.Properties.Remove('resourceVersion')
$secretObject.metadata.PSObject.Properties.Remove('uid')
$secretObject.metadata.PSObject.Properties.Remove('creationTimestamp')
$secretYaml = $secretObject | ConvertTo-Yaml
$secretYaml | kubectl apply -n apicurio -f -

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed copying secret"
    exit 1
}

Write-Host "bootstrap = $env:KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE"

helm upgrade --install apicurio ./infra/k8s/charts/kafka/schema-registry/apicurio `
  --namespace $Namespace `
  -f ./infra/k8s/charts/kafka/schema-registry/apicurio/values.${Environment}.yaml `
  --set kafka.bootstrapServers=$env:KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE `
  --set kafka.clusterCaSecret=$KafkaClusterSecret

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Apicurio Registry deployed successfully!"
} else {
    Write-Host "Failed deploy Apicurio Registry!"
    exit 1
}

Write-Host "------------- REVIEW THIS --------------------"

$env:KAFKA_SCHEMAREGISTRYURL_INTERNAL="apicurio-registry-service:8080/apis/registry"
$env:KAFKA_SCHEMAREGISTRYURL_EXTERNAL="http://registry.localhost:30200/apis/registry"