param(
    [string]$SchemaFile = "_SolutionItems/SchemaRegistry/Sample.json",
    [string]$Subject = "sample-event-value"
)

$SCHEMA_REGISTRY_URL="https://pc-fbb720ab.azure-eastus-kitten-snc.azure.snio.cloud/kafka"
$SR_API_KEY="service-api"
$SR_API_SECRET="eyJhbGciOiJSUzI1NiIsImtpZCI6ImI3OGYyYjM4LTVhZGUtNTlhYy05NjI1LWVlZmQ4MjhhMDY5MiIsInR5cCI6IkpXVCJ9.eyJhdWQiOlsidXJuOnNuOnB1bHNhcjpvLWZnMmo3OmthZmthIl0sImV4cCI6MTc4ODI5MDQwOSwiaHR0cHM6Ly9zdHJlYW1uYXRpdmUuaW8vc2NvcGUiOlsiYWNjZXNzIl0sImh0dHBzOi8vc3RyZWFtbmF0aXZlLmlvL3VzZXJuYW1lIjoic2VydmljZS1hcGlAby1mZzJqNy5hdXRoLnN0cmVhbW5hdGl2ZS5jbG91ZCIsImlhdCI6MTc1Njc1NDQxMiwiaXNzIjoiaHR0cHM6Ly9wYy1mYmI3MjBhYi5henVyZS1lYXN0dXMta2l0dGVuLXNuYy5henVyZS5zbmlvLmNsb3VkL2FwaWtleXMvIiwianRpIjoiMzY5ZDczMzFhODNjNDQ1NmFkNDcwMTZlMzA4NTdmNmEiLCJwZXJtaXNzaW9ucyI6W10sInN1YiI6IlZobmhybUc1WXprZEZiUEtpdVJ2dDZkZjZBV0I3OVhPQGNsaWVudHMifQ.A01yMi92TCVsNACPNZMzdu4zaEseXC-I38BgUfc3EJUNH7Tboyg7nPa1ILLixpUDY2EH7taUD7aMlgtz60YxLiX831xk-RYcdyGojspaMzT733cQkAEacQJrpTOd22esOIj-o2viMKH6wVxuo-QBHSBlJBUt3xWFdcRQloo9ZcTuwD4XlfsCtW4_9yn60BooPaY09RBS9F-toeQdCrD3OxB8Ezj_XhjuFmYuJjhfhTPwqRZ9HrNg2gJ5OnEDcl5FzaCUOEYp6cnl51zL8FNOw1clhFfgzZDBDs0ghdea4_WGmuiYCFqR2RPmkg6oCpnmA1PMtgBqbJUMoUXM6orpvg"


# Загружаем схему
$schemaRaw = Get-Content -Raw -Path $SchemaFile

# Экранируем кавычки и переносы для вставки в JSON-строку
$schemaEscaped = $schemaRaw -replace '"', '\"' -replace "`r?`n", ""

# Формируем тело запроса
$body = @"
{
  "schemaType": "JSON",
  "schema": "$schemaEscaped"
}
"@

Write-Host "➡️  Registering schema from $SchemaFile to subject $Subject ..."

# Делаем POST в Schema Registry
$response = Invoke-RestMethod -Uri "${SCHEMA_REGISTRY_URL}/subjects/${Subject}/versions" `
    -Method Post `
    -Headers @{ "Content-Type" = "application/vnd.schemaregistry.v1+json" } `
    -Body $body `
    -Authentication Basic `
    -Credential (New-Object System.Management.Automation.PSCredential("${SR_API_KEY}", (ConvertTo-SecureString "${SR_API_SECRET}" -AsPlainText -Force)))

Write-Host "✅ Done. Schema Registry response:"
$response | ConvertTo-Json -Depth 10
