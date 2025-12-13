param(
    [string] $Name = "global-vars"
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
