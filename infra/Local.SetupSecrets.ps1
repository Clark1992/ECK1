param(
    [string]$Root = (Get-Location).Path
)

$ErrorActionPreference = "Stop"

Write-Host "Setuping local secrets..."
Write-Host "🔍 Scanning for .csproj files under $Root..." -ForegroundColor Cyan

$userSecretsRoot = Join-Path $env:APPDATA "Microsoft\UserSecrets"

$ingressName = "apicurio-registry-local-dns"
$ingressNamespace = "apicurio"

# Wait for ingress to create
$cmd = "kubectl get ingress $ingressName -n $ingressNamespace --ignore-not-found"
$output = Wait-ForCommand -Command $cmd -TimeoutSeconds 90

$ingressData = kubectl get ingress $ingressName -n $ingressNamespace -o json | ConvertFrom-Json

$SchemaRegistryUrl ="http://$($ingressData.spec.rules.host):$env:INGRESS_HTTP_PORT/apis/ccompat/v7"

$newKafkaConfig = @{
    "BootstrapServers" = $env:KAFKA_EXTERNAL_TLS_BOOTSTRAP
    "SchemaRegistryUrl" = $SchemaRegistryUrl
    "User" = $env:KAFKA_USERNAME
    "Secret" = $env:KAFKA_PASSWORD
}

Get-ChildItem -Path $Root -Recurse -Filter *.csproj | ForEach-Object {
    $projPath = $_.FullName
    Write-Host "`n📄 Processing project: $projPath" -ForegroundColor Yellow

    try {
        [xml]$projXml = Get-Content $projPath
        $secretId = $projXml.Project.PropertyGroup.UserSecretsId
        if (-not $secretId) {
            Write-Host "   ⚪ No UserSecretsId found, skipping."
            return
        }

        $secretPath = Join-Path $userSecretsRoot $secretId
        $secretsFile = Join-Path $secretPath "secrets.json"

        if (-not (Test-Path $secretsFile)) {
            Write-Host "   ⚠️  Secrets file not found for $secretId — creating new."
            New-Item -ItemType Directory -Force -Path $secretPath | Out-Null
            $json = @{}
        }
        else {
            $content = Get-Content $secretsFile -Raw
            if ([string]::IsNullOrWhiteSpace($content)) {
                $json = @{}
            }
            else {
                $json = $content | ConvertFrom-Json
            }
        }

        if (-not ($json.PSObject.Properties.Name -contains 'Kafka')) {
            Write-Host "   ➕ Adding new Kafka section."
            Add-Member -InputObject $json -MemberType NoteProperty -Name "Kafka" -Value ([PSCustomObject]@{}) -Force
        }
        else {
            Write-Host "   🔄 Updating Kafka section."
        }

        foreach ($key in $newKafkaConfig.Keys) {
            $json.Kafka | Add-Member -NotePropertyName $key -NotePropertyValue $newKafkaConfig[$key] -Force
        }

        $json | ConvertTo-Json -Depth 10 | Out-File $secretsFile -Encoding UTF8
        Write-Host "   ✅ Updated $secretsFile" -ForegroundColor Green
    }
    catch {
        Write-Host "   ❌ Error processing ${projPath}: $_" -ForegroundColor Red
    }
}

Write-Host "`n🎉 Done!"
