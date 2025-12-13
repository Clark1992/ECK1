param(
    [string]$SchemaDir
)

$schemaRegistryUrl = $env:KAFKA_SCHEMAREGISTRY_URL
$srApiKey          = $env:KAFKA_USERNAME
$srApiSecret       = $env:KAFKA_PASSWORD

if (-not $schemaRegistryUrl -or -not $srApiKey -or -not $srApiSecret) {
    Write-Error "❌ Required environment variables (SCHEMA_REGISTRY_URL, SR_API_KEY, SR_API_SECRET) are not set."
    exit 1
}

$files = Get-ChildItem -Path $SchemaDir -Recurse -Include *.json, *.avsc, *.proto

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

    $extension = [System.IO.Path]::GetExtension($file.FullName).ToLower()

    switch ($extension) {
        ".json" { $schemaType = "JSON" }
        ".avsc" { $schemaType = "AVRO" }
        ".proto" { $schemaType = "PROTOBUF" }
        default { throw "Unexpected: $extension." }
    }

    $body = @{
      schemaType = $schemaType
      schema     = $schemaRaw
    } | ConvertTo-Json -Compress

    Write-Host "➡️ Registering schema from '$($file.FullName)' as subject '$subject' ..."

    # TODO: wait for service activation if it spinnign up

    # Write-Host " Invoke-RestMethod $schemaRegistryUrl/subjects/$subject/versions `
    #         -Uri "$schemaRegistryUrl/subjects/$subject/versions" `
    #         -Method Post `
    #         -Headers $headers `
    #         -Body $body"
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
