param(
    [string]$KafkaNamespace,
    [string]$Namespace,
    [string]$KafkaClusterName,
    [string]$KafkaUserSecretName,
    [string]$Password
)

Write-Host "Copying secret from $KafkaNamespace to $Namespace namespace"

. "../.github/scripts/common.ps1"
. "../.github/scripts/wait.ps1"

if (-not $KafkaUserSecretName -or -not $Password) {
    Write-Host "❌ KAFKA_USERNAME / KAFKA_PASSWORD NOT SET"
    throw
}

# $secret = kubectl get secret $KafkaUserSecretName -n $KafkaNamespace -o yaml > .github\scripts\test3.yaml

$secret = kubectl get secret $KafkaUserSecretName -n $KafkaNamespace -o json

Clean-Secret $secret | kubectl apply -n $Namespace -f -

$KafkaClusterSecretPattern = "{0}-cluster-ca-cert"
$KafkaClusterSecret = [string]::Format($KafkaClusterSecretPattern, $KafkaClusterName)

$secret = kubectl get secret $KafkaClusterSecret -n $KafkaNamespace -o json
Clean-Secret $secret | kubectl apply -n $Namespace -f -

if ($LASTEXITCODE -ne 0) {
    Write-Host "Failed copying secret"
    throw
}

try {
    $cmd = "kubectl get secret $KafkaClusterSecret -n $Namespace --ignore-not-found"
    WaitFor-Command -Command $cmd -MaxAttempts 10
    Write-Host "✅ ${KafkaClusterSecret}: Secret updated."
} catch {
    Write-Host $_
    Write-Host "❌ ${KafkaClusterSecret}: secret not created!"
    throw
}

# Basic Auth through ingress

# 3️⃣ Generate bcrypt-hash
Write-Host "🔐 Generate bcrypt hash for user '$KafkaUserSecretName'..."

$authClean = (docker run --rm httpd:2.4 htpasswd -nb $KafkaUserSecretName $Password)[0].Trim()

# 4️⃣ Check if secret changed
Write-Host "🔍  Check current secret 'apicurio-basic-auth'..."
$existingSecret = kubectl get secret apicurio-basic-auth -n $Namespace -o json 2>$null
$needUpdate = $true
if ($existingSecret) {
    $existingData = ($existingSecret | ConvertFrom-Json).data.auth
    if ($existingData) {
        $existingDecoded = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($existingData))
        if ($existingDecoded.Trim() -eq $authClean) {
            Write-Host "✅ Secret unchanged"
            $needUpdate = $false
        }
    }
}

# 5️⃣ Update secret
$ingressName = "apicurio-registry"

if ($needUpdate) {
    Write-Host "📦 Recreating secret 'apicurio-basic-auth'..."
    kubectl delete secret apicurio-basic-auth -n $Namespace --ignore-not-found | Out-Null
    kubectl create secret generic apicurio-basic-auth -n $Namespace --from-literal=auth="$authClean" | Out-Null

    try {
        $cmd = "kubectl get secret apicurio-basic-auth -n $Namespace --ignore-not-found"
        WaitFor-Command -Command $cmd -MaxAttempts 10
        Write-Host "✅ apicurio-basic-auth: Secret updated."
    } catch {
        Write-Host $_
        Write-Host "❌ apicurio-basic-auth: secret not created!"
        throw
    }

    #$ts = Get-Date -UFormat %s
    #kubectl annotate ingress $ingressName -n $Namespace refresh-trigger="$ts" --overwrite | Out-Null

    #Write-Host "✅ Ingress refreshed."
} else {
    Write-Host "⏭️  Skip update unchanged secret."
}
