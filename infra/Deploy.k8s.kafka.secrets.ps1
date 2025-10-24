# # Saving creds as k8s secrets to be used by net clients inside k8s (in order not to pass them as env vars)
# # it will have a copy of some data (password) from strimzi managed secret
# param(
#     [string]$Namespace = "default",
#     [string]$SecretName = "kafka-client-secret",
#     [string]$BootstrapServers,
#     [string]$SchemaRegistryUrl,
#     [string]$User = "kafka-user",
#     [string]$Secret
# )

# $ErrorActionPreference = "Stop"

# if (-not $BootstrapServers -or -not $SchemaRegistryUrl -or -not $User -or -not $Secret) {
#     Write-Host "❌ Missing one or more required parameters:"
#     Write-Host "  --BootstrapServers, --SchemaRegistryUrl, --User, --Secret"
#     exit 1
# }

# $yaml = @"
# apiVersion: v1
# kind: Secret
# metadata:
#   name: $SecretName
#   namespace: $Namespace
# type: Opaque
# stringData:
#   Kafka__BootstrapServers: "$BootstrapServers"
#   Kafka__SchemaRegistryUrl: "$SchemaRegistryUrl"
#   Kafka__User: "$User"
#   Kafka__Secret: "$Secret"
# "@

# Write-Host "📦 Applying secret '$SecretName' to namespace '$Namespace'..."
# $yaml | kubectl apply -f -
# Write-Host "✅ Secret applied successfully."
