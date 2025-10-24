. $PSScriptRoot/Wait.ps1
. $PSScriptRoot/Ensure.ps1

function Test-PortInUse {
    param([int]$port)
    try {
        $tcp = New-Object System.Net.Sockets.TcpClient
        $async = $tcp.BeginConnect('127.0.0.1', $port, $null, $null)
        $wait = $async.AsyncWaitHandle.WaitOne(500)
        if ($wait -and $tcp.Connected) { $tcp.Close(); return $true }
        return $false
    } catch {
        return $false
    }
}

function Start-LocalDockerRegistry {
    param (
        [string]$registryName = "local-registry",
        [int]$registryPort = 5000
    )

    $registryRunning = docker ps --filter "name=$registryName" --filter "status=running" --quiet

    if ($registryRunning -and $registryRunning.Contains("failed")) {
        Write-Error "Error detected: $registryRunning"
        throw
    }

    if ([string]::IsNullOrWhiteSpace($registryRunning)) {
        Write-Host "Starting local registry..."
        docker run -d -p "${registryPort}:5000" --restart=always --name $registryName registry:2
    } else {
        Write-Host "Local registry already running."
    }
}

function Build-DockerImage {
    param (
        [Parameter(Mandatory)]
        [string]$imageNameWithTag,

        [Parameter(Mandatory)]
        [string]$dockerfilePath,

        [int]$registryPort = 5000
    )

    Write-Host "Setup global NuGet data as secret for Docker build..."
    $env:DOCKER_BUILDKIT=1
    $globalNugetConfig = "$env:APPDATA\NuGet\NuGet.Config"

    Write-Host "Using NuGet.Config at: $globalNugetConfig"

    if (-Not (Test-Path $globalNugetConfig)) {
        throw "NuGet.Config not found"
    }
    
    if (-not $imageNameWithTag.StartsWith("localhost:", [System.StringComparison]::OrdinalIgnoreCase)) {
        $fullImageName = "localhost:${registryPort}/${imageNameWithTag}"
    }

    Write-Host "Building API image $fullImageName..."

    docker build -t $fullImageName -f $dockerfilePath --secret "id=nugetconfig,src=$globalNugetConfig" ./ --progress=plain

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Docker build failed"
        throw
    }

    Write-Host "Pushing API image to local registry..."
    docker push $fullImageName
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "Docker push failed"; 
        throw 
    }
}

function Setup-GlobalNuget {
    $env:DOCKER_BUILDKIT=1
    $globalNugetConfig = "$env:APPDATA\NuGet\NuGet.Config"

    Write-Host "Using NuGet.Config at: $globalNugetConfig"

    if (-Not (Test-Path $globalNugetConfig)) {
        throw "NuGet.Config not found"
    }
}

function Get-NuGetPackage {
    param(
        [string]$PackageId,
        [string]$Version,
        [string]$Destination
    )

    $nupkgPath = Join-Path $Destination "$PackageId.$Version.nupkg"

    if (-not (Test-Path $nupkgPath)) {
        Write-Host "📦 Downloading $PackageId v$Version..."
        Invoke-WebRequest -Uri "https://www.nuget.org/api/v2/package/$PackageId/$Version" -OutFile $nupkgPath
    }

    Write-Host "📂 Extracting $PackageId..."
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($nupkgPath, $Destination, $true)

    $libPath = Get-ChildItem -Path $Destination -Directory -Recurse | Where-Object { $_.FullName -match "lib\\net" } | Select-Object -First 1
    if ($libPath) {
        Copy-Item "$($libPath.FullName)\*.dll" $Destination -Force
    }
    else {
        Write-Warning "⚠️  Could not find lib/net* folder for $PackageId."
    }
}

function Get-YamlValue {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PropPath,
        
        [Parameter(Mandatory = $true)]
        [string]$YamlPath
    )

    if (-not (Get-Module -ListAvailable -Name powershell-yaml)) {
        Install-Module -Name powershell-yaml -Scope CurrentUser -Force
    }
    Import-Module powershell-yaml -ErrorAction Stop

    if (-not (Test-Path $YamlPath)) {
        throw "YAML file not found: $YamlPath"
    }

    $yaml = Get-Content $YamlPath -Raw | ConvertFrom-Yaml

    $segments = $PropPath -split '\.'

    $current = $yaml
    foreach ($segment in $segments) {
        if ($segment -match '^([^\[]+)\[(\d+)\]$') {
            $key = $matches[1]
            $index = [int]$matches[2]

            if ($current -is [System.Collections.IDictionary]) {
                $current = $current[$key]
            } else {
                throw "Expected a dictionary for key '$key', but got $($current.GetType().Name)"
            }

            if ($null -eq $current -or $index -ge $current.Count) {
                throw "Index [$index] out of range for '$key'"
            }

            $current = $current[$index]
        }
        else {
            if ($current -is [System.Collections.IDictionary]) {
                if ($current.ContainsKey($segment)) {
                    $current = $current[$segment]
                } else {
                    throw "Key '$segment' not found"
                }
            }
            else {
                throw "Cannot access property '$segment' on non-dictionary type '$($current.GetType().Name)'"
            }
        }
    }

    return $current
}

function Clean-Secret {
    param (
        [Parameter(Mandatory = $true, ValueFromPipeline = $true)]
        [object] $Secret
    )

    if ($Secret -is [System.Array]) {
        $jsonString = $Secret -join "`n"
    }
    elseif ($Secret -is [string]) {
        $jsonString = $Secret
    }

    try {
        $secretObject = $jsonString | ConvertFrom-Json -ErrorAction Stop
    } catch {
        throw "❌ Failed to parse JSON secret: $($_.Exception.Message)"
    }

    $propsToRemove = @(
        'ownerReferences',
        'namespace',
        'resourceVersion',
        'uid',
        'creationTimestamp'
    )

    foreach ($prop in $propsToRemove) {
        if ($secretObject.metadata.PSObject.Properties.Name -contains $prop) {
            $secretObject.metadata.PSObject.Properties.Remove($prop)
        }
    }

    return ($secretObject | ConvertTo-Yaml)
}
