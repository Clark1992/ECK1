function Run-DbUp {

    param(
        [string]$ScriptsPath,
        [string]$ConnectionString = $env:ConnectionStrings__DefaultConnection
    )

    if (Get-Module dbops) {
        Remove-Module dbops -Force -ErrorAction SilentlyContinue
    }

    Write-Host "=== Running SQL migrations via dbops ==="

    if (-not $ScriptsPath) {
        Write-Error "❌ Missing parameter: -ScriptsPath"
        exit 1
    }

    if (-not (Test-Path $ScriptsPath)) {
        Write-Error "❌ Scripts path '$ScriptsPath' not found."
        exit 2
    }

    if (-not $ConnectionString) {
        Write-Error "❌ Connection string not provided (use -ConnectionString or set env:ConnectionStrings__DefaultConnection)"
        exit 3
    }

    if (-not (Get-Module -ListAvailable -Name dbops)) {
        Write-Host "📦 Installing dbops PowerShell module..."
        try {
            Install-Module dbops -Scope CurrentUser -Force -AllowClobber -ErrorAction Stop
        }
        catch {
            Write-Error "❌ Failed to install dbops module: $($_.Exception.Message)"
            exit 4
        }
    }
    else {
        Write-Host "🔄 Updating dbops module..."
        Update-Module dbops -Force -ErrorAction SilentlyContinue
    }

    Import-Module dbops -Force

    Write-Host "🚀 Applying migrations from '$ScriptsPath'..."

    try {
        Push-Location $ScriptsPath
        $allScripts = Get-ChildItem -Path . -Filter "*.sql" -File -Recurse | Sort-Object Name

        foreach ($script in $allScripts) {
            Write-Host "`n📄 Running script: $($script.Name)" -ForegroundColor Cyan

            Install-DBOScript `
                -Path $script.FullName `
                -ConnectionString $ConnectionString `
                -Type SqlServer

            Write-Host "✅ $($script.Name) completed successfully."
        }
        
        exit 0
    }
    catch {
        Write-Error "❌ Error running migrations: $($_.Exception.Message)"
        exit 6
    }
    finally {
        Pop-Location
    }
}

function Get-EnvValueFromYaml {
    param(
        [string]$Key,
        [string]$YamlPath
    )
    
    if (-not (Get-Module -ListAvailable -Name powershell-yaml)) {
        Install-Module -Name powershell-yaml -Scope CurrentUser -Force
    }

    Import-Module powershell-yaml

    if (-not (Test-Path $YamlPath)) {
        throw "YAML file not found: $YamlPath"
    }

    $yamlContent = Get-Content $YamlPath -Raw | ConvertFrom-Yaml

    if (-not $yamlContent.env) {
        throw "'env' section not found in YAML"
    }

    $envSection = $yamlContent.env

    if ($envSection.ContainsKey($Key)) {
        return $envSection[$Key]
    }
    else {
        throw "Key '$Key' not found in env section"
    }
}