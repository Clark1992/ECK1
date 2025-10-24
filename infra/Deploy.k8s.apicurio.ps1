# param(
#     [string]$Namespace = "apicurio",
#     [string]$Environment = "local",
#     [string]$KafkaNamespace = "kafka",
#     [string]$KafkaUserSecret = "kafka-user",
#     [string]$KafkaClusterSecretPattern = "{0}-cluster-ca-cert"
# )

# $ErrorActionPreference = "Stop"

# kubectl get ns $Namespace -o name 2>$null
# if ($LASTEXITCODE -ne 0) {
#     kubectl create namespace $Namespace
# }

# Write-Host "Copying secret from $KafkaNamespace to $Namespace namespace"
# # $secret = kubectl get secret $KafkaUserSecret -n $KafkaNamespace -o yaml > .github\scripts\test2.yaml

# $secret = kubectl get secret $KafkaUserSecret -n $KafkaNamespace -o json
# $secretObject = $secret | ConvertFrom-Json
# $secretObject.metadata.PSObject.Properties.Remove('ownerReferences')
# $secretObject.metadata.PSObject.Properties.Remove('namespace')
# $secretObject.metadata.PSObject.Properties.Remove('resourceVersion')
# $secretObject.metadata.PSObject.Properties.Remove('uid')
# $secretObject.metadata.PSObject.Properties.Remove('creationTimestamp')
# $secretYaml = $secretObject | ConvertTo-Yaml
# $secretYaml | kubectl apply -n apicurio -f -

# $KafkaClusterSecret = [string]::Format($KafkaClusterSecretPattern, $env:KAFKA_CLUSTER)

# $secret = kubectl get secret $KafkaClusterSecret -n $KafkaNamespace -o json
# $secretObject = $secret | ConvertFrom-Json
# $secretObject.metadata.PSObject.Properties.Remove('ownerReferences')
# $secretObject.metadata.PSObject.Properties.Remove('namespace')
# $secretObject.metadata.PSObject.Properties.Remove('resourceVersion')
# $secretObject.metadata.PSObject.Properties.Remove('uid')
# $secretObject.metadata.PSObject.Properties.Remove('creationTimestamp')
# $secretYaml = $secretObject | ConvertTo-Yaml
# $secretYaml | kubectl apply -n apicurio -f -

# if ($LASTEXITCODE -ne 0) {
#     Write-Host "Failed copying secret"
#     throw
# }

# Write-Host "bootstrap = $env:KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE"

# helm upgrade --install apicurio ./infra/k8s/charts/kafka/schema-registry/apicurio `
#   --namespace $Namespace `
#   -f ./infra/k8s/charts/kafka/schema-registry/apicurio/values.${Environment}.yaml `
#   --set kafka.bootstrapServers=$env:KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE `
#   --set kafka.clusterCaSecret=$KafkaClusterSecret

# if ($LASTEXITCODE -eq 0) {
#     Write-Host "✅ Apicurio Registry deployed successfully!"
# } else {
#     Write-Host "Failed deploy Apicurio Registry!"
#     throw
# }

# # Basic Auth through ingress

# if (-not $env:KAFKA_USERNAME -or -not $env:KAFKA_PASSWORD) {
#     Write-Host "❌ KAFKA_USERNAME / KAFKA_PASSWORD NOT SET"
#     throw
# }

# $Username = $env:KAFKA_USERNAME
# $Password = $env:KAFKA_PASSWORD

# # 3️⃣ Generate bcrypt-hash
# Write-Host "🔐 Generate bcrypt hash for user '$Username'..."

# $authClean = (docker run --rm httpd:2.4 htpasswd -nb $Username $Password)[0].Trim()

# # 4️⃣ Check if secret changed
# Write-Host "🔍  Check current secret 'apicurio-basic-auth'..."
# $existingSecret = kubectl get secret apicurio-basic-auth -n $Namespace -o json 2>$null
# $needUpdate = $true
# if ($existingSecret) {
#     $existingData = ($existingSecret | ConvertFrom-Json).data.auth
#     if ($existingData) {
#         $existingDecoded = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($existingData))
#         if ($existingDecoded.Trim() -eq $authClean) {
#             Write-Host "✅ Secret unchanged"
#             $needUpdate = $false
#         }
#     }
# }

# # 5️⃣ Update secret
# $ingressName = "apicurio-registry"

# if ($needUpdate) {
#     Write-Host "📦 Recreating secret 'apicurio-basic-auth'..."
#     kubectl delete secret apicurio-basic-auth -n $Namespace --ignore-not-found | Out-Null
#     kubectl create secret generic apicurio-basic-auth -n $Namespace --from-literal=auth="$authClean" | Out-Null

#     $maxRetries = 10
#     for ($i=0; $i -lt $maxRetries; $i++) {
#         $secretCheck = kubectl get secret apicurio-basic-auth -n $Namespace --ignore-not-found
#         if ($secretCheck) { break }
#         Start-Sleep -Seconds $WaitSeconds
#     }
#     if (-not $secretCheck) {
#         Write-Host "❌ Secret didnt appear in $($maxRetries*$WaitSeconds) sec"
#         throw
#     }

#     Write-Host "✅ Secret updated."

#     #$ts = Get-Date -UFormat %s
#     #kubectl annotate ingress $ingressName -n $Namespace refresh-trigger="$ts" --overwrite | Out-Null

#     #Write-Host "✅ Ingress refreshed."
# } else {
#     Write-Host "⏭️  Skip update unchanged secret."
# }

# Write-Host "`n🎉 Done!"