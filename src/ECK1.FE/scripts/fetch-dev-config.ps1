# Fetches Zitadel OIDC config from the local K8s cluster
# and writes it to .env.development.local for local dev server use.
$ErrorActionPreference = 'Stop'

# Read Zitadel authority (external issuer URL) from global-vars configmap
$zitadelAuthority = kubectl get configmap global-vars -n app-services -o jsonpath='{.data.Zitadel__Issuer}' 2>$null
if (-not $zitadelAuthority) {
    Write-Error "Could not read Zitadel__Issuer from global-vars configmap. Is the cluster running with infra deployed?"
    exit 1
}

# Read OIDC client_id from secret
$clientIdB64 = kubectl get secret zitadel-oidc-client -n zitadel -o jsonpath='{.data.client_id}' 2>$null
if (-not $clientIdB64) {
    Write-Error "Could not find zitadel-oidc-client secret. Is the cluster running with Zitadel deployed?"
    exit 1
}
$clientId = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($clientIdB64))

$envFile = Join-Path $PSScriptRoot ".." ".env.development.local"
Set-Content -Path $envFile -Value @"
VITE_ZITADEL_AUTHORITY=$zitadelAuthority
VITE_ZITADEL_CLIENT_ID=$clientId
"@

Write-Host "Wrote OIDC config to .env.development.local" -ForegroundColor Green
Write-Host "  Authority: $zitadelAuthority" -ForegroundColor Gray
Write-Host "  Client ID: $clientId" -ForegroundColor Gray
