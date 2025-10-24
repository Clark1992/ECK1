function Ensure-Executable {
    param (
        [string]$exeName,          # Executable name (w/o .exe)
        [string]$version,          # Version
        [string]$urlTemplate,      # URL с {0} as version placeholder
        [string]$archiveSubPath = ""  # path inside ZIP (is needed)
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

    $url = $urlTemplate -f $version
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

    $exePath = if ($archiveSubPath) {
        Join-Path $installDir "$archiveSubPath\$exeName.exe"
        Write-Host Here1
    } else {
        Join-Path $installDir "$exeName.exe"
        Write-Host Here2
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
                      -urlTemplate "https://get.helm.sh/helm-{0}-windows-amd64.zip" `
                      -archiveSubPath "windows-amd64"
}

# ===========================================
# Ensure Helmfile
# ===========================================
function Ensure-Helmfile {
    param([string]$helmfileVersion = "1.1.7")
    Ensure-Executable -exeName "helmfile" `
                      -version $helmfileVersion `
                      -urlTemplate "https://github.com/helmfile/helmfile/releases/download/v{0}/helmfile_{0}_windows_amd64.tar.gz"
}

# ===========================================
# Ensure Kubectl
# ===========================================
function Ensure-Kubectl {
    try {
        $version = "v1.31.0"
    } catch {
        Write-Error "❌ Failed to fetch latest kubectl version. $_"
        throw
    }

    $urlTemplate = "https://dl.k8s.io/release/{0}/bin/windows/amd64/kubectl.exe"

    Ensure-Executable -exeName "kubectl" `
                      -version $version `
                      -urlTemplate $urlTemplate
}

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

    $helmDiffUrl = "https://github.com/databus23/helm-diff/releases/download/$helmVersion/helm-diff-windows-amd64.tgz"
    $pluginDir = Join-Path $env:APPDATA "helm\plugins\helm-diff"
    $binDir = Join-Path $pluginDir "bin"

    if (-not (Test-Path $binDir)) {
        Write-Host "Creating bin folder at $binDir"
        New-Item -ItemType Directory -Path $binDir | Out-Null
    }

    $tgzPath = Join-Path $env:TEMP "helm-diff-windows.tgz"
    Write-Host "Downloading helm-diff Windows release..."
    Invoke-WebRequest -Uri $helmDiffUrl -OutFile $tgzPath

    $extractDir = Join-Path $env:TEMP "helm-diff-windows"
    if (Test-Path $extractDir) { Remove-Item $extractDir -Recurse -Force }
    New-Item -ItemType Directory -Path $extractDir | Out-Null

    Write-Host "Extracting diff.exe..."
    tar -xzf $tgzPath -C $extractDir

    $diffExe = Get-ChildItem -Path $extractDir -Recurse -Filter "diff.exe" | Select-Object -First 1
    if (-not $diffExe) {
        Write-Error "❌ diff.exe not found in the downloaded archive"
        return
    }

    Copy-Item $diffExe.FullName -Destination $binDir -Force
    Write-Host "✅ diff.exe copied to $binDir"

    Write-Host "✅ Helm plugin 'diff' installed successfully."
}
