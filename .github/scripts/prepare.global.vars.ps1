param(
    [string] $Name = "global-vars",
    [string] $SecretName = "global-secrets"
)

$GlobalNamespace = "global"

Write-Host "Preparing global vars"

$cmJson = kubectl get configmap $Name -n $GlobalNamespace -o json

$cm = $cmJson | ConvertFrom-Json

if (-not $cm.data) {
    Write-Error "ConfigMap '$Name' in Namespace '$GlobalNamespace' has no .data section"
    exit 1
}

foreach ($key in $cm.data.PSObject.Properties.Name) {
    $value = $cm.data.$key

    [System.Environment]::SetEnvironmentVariable($key, $value, "Process")

    Write-Output "Set env: $key=$value"
}

Write-Output "`nAll keys from ConfigMap '$Name' applied as environment variables."

$secretJson = kubectl get secret $SecretName -n $GlobalNamespace -o json
$secret = $secretJson | ConvertFrom-Json

if (-not $secret.data) {
    Write-Error "Secret '$SecretName' in Namespace '$GlobalNamespace' has no .data section"
    exit 1
}

foreach ($key in $secret.data.PSObject.Properties.Name) {
    $encodedValue = $secret.data.$key
    $decodedBytes = [System.Convert]::FromBase64String($encodedValue)
    $value = [System.Text.Encoding]::UTF8.GetString($decodedBytes)

    [System.Environment]::SetEnvironmentVariable($key, $value, "Process")

    Write-Output "Set secret env: $key=$value"
}

Write-Output "`nAll keys from Secret '$SecretName' applied as environment variables."
