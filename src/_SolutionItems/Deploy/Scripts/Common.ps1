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
        exit 1
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
        exit 1
    }

    Write-Host "Pushing API image to local registry..."
    docker push $fullImageName
    if ($LASTEXITCODE -ne 0) { 
        Write-Error "Docker push failed"; 
        exit 1 
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
