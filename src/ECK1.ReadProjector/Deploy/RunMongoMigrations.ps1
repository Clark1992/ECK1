param (
    [string]$WorkingDir = (Get-Location)
)

Push-Location $WorkingDir

Write-Host "Run migrations."

if (-not (Get-Command migrate-mongo -ErrorAction SilentlyContinue)) {
    npm install -g migrate-mongo
}

migrate-mongo up

Pop-Location

Write-Host "Run migrations: Done."