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

function Ensure-Helm {
    param (
        [string]$helmVersion = "v3.12.0"
    )

    $helmZipName = "helm-$helmVersion-windows-amd64.zip"
    $zipPath     = "$env:TEMP\$helmZipName"
    $extractPath = "$env:TEMP\helm-$helmVersion"
    $helmExe     = "$extractPath\windows-amd64\helm.exe"

    if (-not (Get-Command helm -ErrorAction SilentlyContinue)) {
        Write-Host "Helm not found in system. Using temporary path."

        # Download the ZIP if it's not already downloaded
        if (-not (Test-Path $zipPath)) {
            Write-Host "Downloading Helm ZIP..."
            $helmUrl = "https://get.helm.sh/$helmZipName"
            Invoke-WebRequest -Uri $helmUrl -OutFile $zipPath
        } else {
            Write-Host "Helm ZIP already downloaded: $zipPath"
        }

        # Extract the ZIP if it's not already extracted
        if (-not (Test-Path $helmExe)) {
            Write-Host "Extracting Helm..."
            Expand-Archive -Path $zipPath -DestinationPath $extractPath -Force
        } else {
            Write-Host "Helm already extracted: $helmExe"
        }

        # Temporarily add Helm to the PATH for the current session
        $env:PATH = "$extractPath\windows-amd64;" + $env:PATH
        Write-Host "Helm added to temporary PATH: $helmExe"
    } else {
        Write-Host "Helm found in system: $(Get-Command helm)"
    }
}

function Ensure-Kubectl {
    $kubectlExists = Get-Command kubectl -ErrorAction SilentlyContinue

    if ($kubectlExists) {
        Write-Host "✅ kubectl is already installed."
        return
    }

    Write-Host "⚠ kubectl not found. Installing to temp folder..."

    $installDir = Join-Path $env:TEMP "kubectl_install"
    if (-not (Test-Path $installDir)) {
        New-Item -ItemType Directory -Path $installDir | Out-Null
    }

    $version = (Invoke-RestMethod -Uri "https://storage.googleapis.com/kubernetes-release/release/stable.txt").Trim()
    $url = "https://dl.k8s.io/release/$version/bin/windows/amd64/kubectl.exe"
    $kubectlPath = Join-Path $installDir "kubectl.exe"

    Write-Host "Downloading kubectl $version..."
    Invoke-WebRequest -Uri $url -OutFile $kubectlPath

    $env:PATH = "$installDir;$env:PATH"

    Write-Host "✅ kubectl installed to temporary folder: $kubectlPath"
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

function Wait-ForCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,
        [int]$TimeoutSeconds = 60,
        [int]$IntervalSeconds = 2
    )

    $elapsed = 0

    while ($true) {
        try {
            $output = Invoke-Expression $Command

            if ($output -and $LASTEXITCODE -eq 0) {
                return $output
            }
        }
        catch {
        }

        if ($elapsed -ge $TimeoutSeconds) {
            throw "Timeout: $TimeoutSeconds seconds.`nCommand: $Command"
        }

        Start-Sleep -Seconds $IntervalSeconds
        $elapsed += $IntervalSeconds
    }
}
