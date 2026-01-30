function Get-Platform {
    if ($IsWindows) {
        $platform = "windows"
    } elseif ($IsLinux) {
        $platform = "linux"
    } elseif ($IsMacOS) {
        $platform = "darwin"
    } else {
        throw "Unsupported OS"
    }

    return $platform
}

function Get-Arch {

    switch ($arch = $env:PROCESSOR_ARCHITECTURE.ToLower()) {
        "amd64" { $cpu = "amd64" }
        "x86_64" { $cpu = "amd64" }
        "arm64" { $cpu = "arm64" }
        default  { throw "Unsupported architecture: $arch" }
    }

    return $cpu
}

function Ensure-Executable {
    param (
        [string]$exeName,          # Executable name (w/o .exe)
        [string]$version,          # Version
        [string]$urlTemplate,      # URL с {0} as version placeholder
        [string]$archiveSubPathTemplate = ""  # path inside ZIP (is needed)
    )

    if (Get-Command $exeName -ErrorAction SilentlyContinue) {
        Write-Host "✅ $exeName is already available in PATH." -ForegroundColor Green
        return
    }

    Write-Host "⚠ $exeName not found. Attempting installation..." -ForegroundColor Yellow

    $installDir = Join-Path $env:TEMP "$exeName-$version"
    if (-not (Test-Path $installDir)) {
        New-Item -ItemType Directory -Path $installDir | Out-Null
    }

    $platform = Get-Platform
    $arch = Get-Arch

    $url = $urlTemplate -f $platform, $arch, $version
    $lowerUrl = $url.ToLower()

    $isZip     = $lowerUrl.EndsWith(".zip")
    $isTarGz   = $lowerUrl.EndsWith(".tar.gz")
    $isExe     = $lowerUrl.EndsWith(".exe")

    $extension = if ($isZip) { ".zip" }
             elseif ($isTarGz) { ".tar.gz" }
             elseif ($isExe) { ".exe" }
             else { ".bin" }

    $downloadFileName = "$exeName-$version$extension"
    $downloadPath = Join-Path $env:TEMP $downloadFileName

    $archiveSubPath = $archiveSubPathTemplate -f $platform, $arch

    $exePath = if ($archiveSubPath) {
        Join-Path $installDir "$archiveSubPath\$exeName.exe"
    } else {
        Join-Path $installDir "$exeName.exe"
    }

    # Download and unzip
    if (-not (Test-Path $exePath)) {
        Write-Host "⬇ Downloading $exeName $version from $url..." -ForegroundColor Cyan
        try {
            Invoke-WebRequest -Uri $url -OutFile $downloadPath -UseBasicParsing
        } catch {
            Write-Error "❌ Failed to download $exeName from $url. $_"
            throw
        }

        if ($isZip) {
            Write-Host "📦 Extracting ZIP..." -ForegroundColor Cyan
            Expand-Archive -Path $downloadPath -DestinationPath $installDir -Force
        }
        elseif ($isTarGz) {
            Write-Host "📦 Extracting TAR.GZ..." -ForegroundColor Cyan

            $tarExe = Get-Command tar -ErrorAction SilentlyContinue
            if (-not $tarExe) {
                Write-Error "❌ 'tar' is not available in PATH. Cannot extract .tar.gz"
                throw
            }

            try {
                & tar -xzf $downloadPath -C $installDir
            } catch {
                Write-Error "❌ Failed to extract TAR.GZ archive. $_"
                throw
            }
        }
        elseif ($isExe) {
            Copy-Item -Path $downloadPath -Destination $exePath -Force
        }
        else {
            Write-Error "❌ Unknown file format for $url"
            throw
        }
    } else {
        Write-Host "📁 Found existing $exeName at $exePath"
    }

    # Add to PATH
    $exeFolder = Split-Path $exePath
    if ($env:PATH -notlike "*$exeFolder*") {
        $env:PATH = "$exeFolder;$env:PATH"
        Write-Host "✅ $exeName installed and added to PATH (session only): $exePath" -ForegroundColor Green
    } else {
        Write-Host "ℹ $exeName already in PATH." -ForegroundColor DarkGray
    }
}

# ===========================================
# Ensure Helm
# ===========================================
function Ensure-Helm {
    param([string]$helmVersion = "v3.12.0")
    Ensure-Executable -exeName "helm" `
                      -version $helmVersion `
                      -urlTemplate "https://get.helm.sh/helm-{2}-{0}-{1}.zip" `
                      -archiveSubPathTemplate "{0}-{1}"
}

# ===========================================
# Ensure DbUp Image
# ===========================================
function Ensure-DbUpImage {
    param(
        [string]$ImageName = "dbup",
        [string]$ImageTag = "dev",
        [string]$Registry = "localhost:5000"
    )

    $fullImage = "$Registry/${ImageName}:$ImageTag"

    $exists = docker images --format "{{.Repository}}:{{.Tag}}" | Where-Object { $_ -eq $fullImage }
    if ($exists) {
        Write-Host "✅ DbUp image found locally: $fullImage" -ForegroundColor Green
        return
    }

    Write-Host "⚠ DbUp image not found. Building and pushing: $fullImage" -ForegroundColor Yellow
    & "$PSScriptRoot\..\..\tools\db-up\build-dbup-image.ps1" -ImageName $ImageName -ImageTag $ImageTag -Registry $Registry
    if ($LASTEXITCODE -ne 0) {
        Write-Error "❌ Failed to build/push DbUp image"
        throw
    }
}

# ===========================================
# Ensure Helmfile
# ===========================================
function Ensure-Helmfile {
    param([string]$helmfileVersion = "1.1.7")
    Ensure-Executable -exeName "helmfile" `
                      -version $helmfileVersion `
                      -urlTemplate "https://github.com/helmfile/helmfile/releases/download/v{2}/helmfile_{2}_{0}_{1}.tar.gz"
}

# ===========================================
# Ensure Kubectl
# ===========================================
function Ensure-Kubectl {
    $version = "v1.31.0"
    $urlTemplate = "https://dl.k8s.io/release/{2}/bin/{0}/{1}/kubectl.exe"

    Ensure-Executable -exeName "kubectl" `
                      -version $version `
                      -urlTemplate $urlTemplate
}

# ===========================================
# Ensure Kubectl
# ===========================================
function Ensure-Gomplate {
    $version = "v4.3.3"
    $urlTemplate = "https://github.com/hairyhenderson/gomplate/releases/download/{2}/gomplate_{0}-{1}.exe"

    Ensure-Executable -exeName "gomplate" `
                      -version $version `
                      -urlTemplate $urlTemplate
}

# ===========================================
# Ensure HelmDiff
# ===========================================
function Ensure-HelmDiff {
    param([string]$helmVersion = "v3.12.0")
    # Check if helm is installed
    $helmCmd = Get-Command helm -ErrorAction SilentlyContinue
    if (-not $helmCmd) {
        Write-Error "Helm is not installed. Run Ensure-Helm first."
        return
    }

    # Check if helm-diff plugin is already installed
    $plugins = helm plugin list
    if ($plugins | Where-Object { $_ -match "diff" }) {
        Write-Host "✅ Helm plugin 'diff' is already installed."
        return
    }

    Write-Host "⚠ Helm plugin 'diff' not found. Installing..."
    try {
        helm plugin install https://github.com/databus23/helm-diff --version $helmVersion -q 2>$null
    }
    catch {
        Write-Error "❌ Failed to install helm plugin 'diff': $_"
        exit 1
    }

    #this is workaround
    # https://github.com/databus23/helm-diff/issues/316#issuecomment-1814806292

    $platform = Get-Platform
    $arch = Get-Arch
    $helmDiffUrl = "https://github.com/databus23/helm-diff/releases/download/$helmVersion/helm-diff-$platform-$arch.tgz"
    $pluginDir = Join-Path $env:APPDATA "helm\plugins\helm-diff"
    $binDir = Join-Path $pluginDir "bin"

    if (-not (Test-Path $binDir)) {
        Write-Host "Creating bin folder at $binDir"
        New-Item -ItemType Directory -Path $binDir | Out-Null
    }

    $tgzPath = Join-Path $env:TEMP "helm-diff-$platform.tgz"
    Write-Host "Downloading helm-diff $platform release..."
    Invoke-WebRequest -Uri $helmDiffUrl -OutFile $tgzPath

    $extractDir = Join-Path $env:TEMP "helm-diff-$platform"
    if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
    New-Item -ItemType Directory -Path $extractDir | Out-Null

    Write-Host "Extracting diff binary..."
    tar -xzf $tgzPath -C $extractDir

    $diffBinary = Get-ChildItem -Path $extractDir -Recurse |
        Where-Object { $_.Name -like "diff*" -and -not $_.PSIsContainer } |
        Select-Object -First 1

    if (-not $diffBinary) {
        Write-Error "❌ diff binary not found in the downloaded archive"
        return
    }

    Copy-Item $diffBinary.FullName -Destination $binDir -Force
    Write-Host "✅ diff binary copied to $binDir"

    Write-Host "✅ Helm plugin 'diff' installed successfully."
}
