# Start the frontend dev server with local K8s backend.
# Fetches all required config (Zitadel authority, OIDC client_id) from the K8s cluster.
# Usage:
#   npm start
#   .\scripts\start-dev.ps1
$ErrorActionPreference = 'Stop'

$feDir = $PSScriptRoot | Split-Path

# Fetch OIDC config from K8s
& "$PSScriptRoot\fetch-dev-config.ps1"

# Install deps if needed
Push-Location $feDir
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "Installing npm dependencies..." -ForegroundColor Cyan
        npm ci
    }

    Write-Host "Starting Vite dev server..." -ForegroundColor Green
    npx vite
} finally {
    Pop-Location
}
