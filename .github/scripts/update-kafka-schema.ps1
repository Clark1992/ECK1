param(
    [string]$SchemaDir
)

$schemaRegistryUrl = $env:KAFKA__SCHEMAREGISTRYURL.Trim('"')
$srApiKey          = $env:KAFKA__USER
$srApiSecret       = $env:KAFKA__SECRET

if (-not $schemaRegistryUrl -or -not $srApiKey -or -not $srApiSecret) {
    Write-Error "❌ Required environment variables (SCHEMA_REGISTRY_URL, SR_API_KEY, SR_API_SECRET) are not set."
    exit 1
}

$files = Get-ChildItem -Path $SchemaDir -Recurse -Include *.json, *.avsc

if (-not $files) {
    Write-Error "❌ No schema files found in $SchemaDir"
    exit 1
}

$auth = [System.Convert]::ToBase64String(
      [System.Text.Encoding]::ASCII.GetBytes("${srApiKey}:${srApiSecret}")
  )

$headers = @{
  "Authorization" = "Basic $auth"
  "Content-Type"  = "application/vnd.schemaregistry.v1+json"
}

foreach ($file in $files) {
    $schemaRaw = Get-Content -Raw -Path $file.FullName

    $subject = [System.IO.Path]::GetFileNameWithoutExtension($file.Name)

    $body = @{
      schemaType = "JSON"
      schema     = $schemaRaw
    } | ConvertTo-Json -Compress

    Write-Host "➡️ Registering schema from '$($file.FullName)' as subject '$subject' ..."

    try {
        $response = Invoke-RestMethod `
            -Uri "$schemaRegistryUrl/subjects/$subject/versions" `
            -Method Post `
            -Headers $headers `
            -Body $body

        Write-Host "✅ Done. Schema Registry response:"
        $response | ConvertTo-Json -Depth 10
    }
    catch {
        Write-Error "❌ Failed for subject '$subject': $($_.Exception.Message)"
        if ($_.ErrorDetails.Message) { Write-Host $_.ErrorDetails.Message }
        exit 1
    }
}
