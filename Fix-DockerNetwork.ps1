<#
.SYNOPSIS
    Fixes Docker Desktop / WSL2 networking issues without restarting the entire k8s cluster.
    
.DESCRIPTION
    When Docker Desktop (WSL2) loses outbound connectivity after restart/sleep 
    (image pulls fail, pods can't reach external APIs, Doppler secrets timeout),
    this script restarts only the Docker Desktop engine (not the full WSL/k8s stack)
    and optionally restarts crashing pods.
    
    ROOT CAUSE: Docker Desktop's lifecycle-server injects http_proxy/https_proxy env vars
    pointing to http.docker.internal:3128 into dockerd. After restart/sleep, vpnkit's 
    internal DNS can fail to resolve this hostname, causing ALL outbound traffic from 
    dockerd to timeout. Additionally, /etc/resolv.conf can be a broken symlink.
    
    PERMANENT FIX APPLIED: settings.json proxyHttpMode set to "manual" with no proxy,
    preventing proxy env var injection. This script handles the remaining transient issues.
    
.PARAMETER Action
    diagnose     = Show current network state (default)
    fix          = Restart Docker Desktop engine, fix DNS, restart crashing pods (RECOMMENDED)
    restart-pods = Just restart CrashLoopBackOff / ImagePullBackOff pods
    
.EXAMPLE
    .\Fix-DockerNetwork.ps1 -Action fix
    # Full fix: restart Docker engine + fix DNS + restart crashing pods
    
.EXAMPLE
    .\Fix-DockerNetwork.ps1 -Action diagnose
    # Just show current state without changing anything
#>
param(
    [ValidateSet("diagnose", "fix", "restart-pods")]
    [string]$Action = "diagnose"
)

$ErrorActionPreference = "Stop"

function Test-DockerReady {
    try {
        docker info 2>&1 | Out-Null
        return $LASTEXITCODE -eq 0
    }
    catch { return $false }
}

function Test-DockerPull {
    try {
        docker pull busybox:1.36 2>&1 | Out-Null
        return $LASTEXITCODE -eq 0
    }
    catch { return $false }
}

function Get-CrashingPods {
    $allPods = kubectl get pods --all-namespaces -o json 2>&1 | ConvertFrom-Json
    $result = @()
    foreach ($item in $allPods.items) {
        $containerStatuses = @()
        if ($item.status.containerStatuses) { $containerStatuses += $item.status.containerStatuses }
        if ($item.status.initContainerStatuses) { $containerStatuses += $item.status.initContainerStatuses }
        
        foreach ($cs in $containerStatuses) {
            $reason = $cs.state.waiting.reason
            if ($reason -in @("CrashLoopBackOff", "ImagePullBackOff", "ErrImagePull", "CreateContainerConfigError", "Error")) {
                $result += @{ Namespace = $item.metadata.namespace; Name = $item.metadata.name; Reason = $reason }
                break
            }
        }
    }
    return $result
}

function Wait-ForDocker {
    param([int]$TimeoutSeconds = 180)
    
    Write-Host "  Waiting for Docker daemon..." -ForegroundColor Gray
    $elapsed = 0
    while ($elapsed -lt $TimeoutSeconds) {
        if (Test-DockerReady) {
            Write-Host "  Docker daemon ready ($elapsed s)" -ForegroundColor Green
            return $true
        }
        Start-Sleep -Seconds 5
        $elapsed += 5
        if ($elapsed % 30 -eq 0) { Write-Host "  Still waiting... ($elapsed s)" -ForegroundColor Gray }
    }
    Write-Host "  Docker daemon did not start within $TimeoutSeconds s" -ForegroundColor Red
    return $false
}

function Show-Diagnosis {
    Write-Host "`n=== Docker Desktop WSL2 Network Diagnosis ===" -ForegroundColor Cyan

    # 1. Docker daemon
    Write-Host "`n[1] Docker daemon:" -ForegroundColor Yellow
    if (Test-DockerReady) {
        Write-Host "    OK - daemon responding" -ForegroundColor Green
    } else {
        Write-Host "    NOT RESPONDING" -ForegroundColor Red
        Write-Host "    Run: .\Fix-DockerNetwork.ps1 -Action fix" -ForegroundColor Yellow
        return
    }

    # 2. Docker pull test
    Write-Host "`n[2] Docker pull connectivity:" -ForegroundColor Yellow
    if (Test-DockerPull) {
        Write-Host "    OK - can reach Docker Hub" -ForegroundColor Green
    } else {
        Write-Host "    BROKEN - cannot reach Docker Hub" -ForegroundColor Red
        Write-Host "    Run: .\Fix-DockerNetwork.ps1 -Action fix" -ForegroundColor Yellow
    }

    # 3. Proxy settings check
    Write-Host "`n[3] Docker Desktop proxy config:" -ForegroundColor Yellow
    $settingsPath = "$env:APPDATA\Docker\settings.json"
    if (Test-Path $settingsPath) {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        $mode = $settings.proxyHttpMode
        Write-Host "    proxyHttpMode: $mode" -ForegroundColor White
        if ($mode -eq "system") {
            Write-Host "    WARNING: 'system' mode causes proxy env injection into dockerd" -ForegroundColor Red
            Write-Host "    The 'fix' action will set this to 'manual' (no proxy)" -ForegroundColor Yellow
        } elseif ($mode -eq "manual") {
            Write-Host "    OK - manual mode, no proxy injection" -ForegroundColor Green
        }
    }

    # 4. Dockerd proxy env vars
    Write-Host "`n[4] Dockerd proxy env vars:" -ForegroundColor Yellow
    try {
        $dockerdPid = wsl -d docker-desktop -- sh -c "pgrep -f '/usr/local/bin/dockerd' | head -1" 2>&1
        if ($dockerdPid -match '^\d+$') {
            $proxyEnv = wsl -d docker-desktop -- sh -c "cat /proc/$dockerdPid/environ 2>/dev/null | tr '\0' '\n' | grep -i proxy" 2>&1
            if ($proxyEnv) {
                $proxyEnv | ForEach-Object { Write-Host "    $_" -ForegroundColor Red }
                Write-Host "    ^ Proxy env vars present - this is the problem!" -ForegroundColor Red
            } else {
                Write-Host "    None - OK" -ForegroundColor Green
            }
        }
    } catch {
        Write-Host "    Could not check (WSL not responding?)" -ForegroundColor Gray
    }

    # 5. Crashing pods
    Write-Host "`n[5] Crashing/Error pods:" -ForegroundColor Yellow
    try {
        $crashing = Get-CrashingPods
        if ($crashing.Count -eq 0) {
            Write-Host "    None" -ForegroundColor Green
        } else {
            foreach ($p in $crashing) {
                Write-Host "    $($p.Namespace)/$($p.Name) - $($p.Reason)" -ForegroundColor Red
            }
        }
    } catch {
        Write-Host "    Could not query k8s (docker not ready?)" -ForegroundColor Gray
    }

    Write-Host ""
}

function Invoke-RestartCrashingPods {
    Write-Host "`n=== Restarting crashing pods ===" -ForegroundColor Cyan
    $crashing = Get-CrashingPods
    if ($crashing.Count -eq 0) {
        Write-Host "  No crashing pods found." -ForegroundColor Green
        return
    }
    Write-Host "  Found $($crashing.Count) crashing pods. Deleting (controllers will recreate)..."
    foreach ($pod in $crashing) {
        Write-Host "  $($pod.Namespace)/$($pod.Name) ($($pod.Reason))"
        kubectl delete pod $pod.Name -n $pod.Namespace --grace-period=0 --force 2>&1 | Out-Null
    }
    Write-Host "  Done. Pods will be recreated by their controllers." -ForegroundColor Green
}

function Invoke-Fix {
    Write-Host "`n=== Fixing Docker Desktop Networking ===" -ForegroundColor Cyan
    
    # Step 1: Ensure proxy mode is "manual"
    Write-Host "`n[Step 1] Checking proxy settings..." -ForegroundColor Yellow
    $settingsPath = "$env:APPDATA\Docker\settings.json"
    $needsRestart = $false
    
    if (Test-Path $settingsPath) {
        $settings = Get-Content $settingsPath -Raw | ConvertFrom-Json
        if ($settings.proxyHttpMode -ne "manual") {
            Write-Host "  Setting proxyHttpMode to 'manual' (prevents proxy env injection)" -ForegroundColor White
            $settings.proxyHttpMode = "manual"
            $settings | ConvertTo-Json -Depth 10 | Set-Content $settingsPath -Encoding UTF8
            $needsRestart = $true
        } else {
            Write-Host "  Already set to 'manual' - OK" -ForegroundColor Green
        }
    }
    
    # Step 2: Check if docker pull works already
    if (-not $needsRestart -and (Test-DockerReady)) {
        Write-Host "`n[Step 2] Testing current connectivity..." -ForegroundColor Yellow
        if (Test-DockerPull) {
            Write-Host "  Docker pull works! No engine restart needed." -ForegroundColor Green
            Invoke-RestartCrashingPods
            return
        }
        Write-Host "  Docker pull failed - restarting Docker engine..." -ForegroundColor Red
    }
    
    # Step 3: Restart Docker Desktop engine (NOT the WSL/k8s cluster)
    Write-Host "`n[Step 3] Restarting Docker Desktop engine..." -ForegroundColor Yellow
    Write-Host "  This restarts only Docker Desktop, NOT the k8s cluster." -ForegroundColor Gray
    Write-Host "  Kubernetes state (etcd, pods, volumes) is preserved." -ForegroundColor Gray
    
    # Kill Docker Desktop processes
    $dockerProcs = @("Docker Desktop", "com.docker.backend", "com.docker.build", "com.docker.dev-envs", "com.docker.extensions")
    foreach ($proc in $dockerProcs) {
        Get-Process $proc -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 3
    
    # Start Docker Desktop
    Write-Host "  Starting Docker Desktop..." -ForegroundColor White
    Start-Process "C:\Program Files\Docker\Docker\Docker Desktop.exe"
    
    if (-not (Wait-ForDocker -TimeoutSeconds 180)) {
        Write-Host "`n  Docker Desktop failed to start. Try 'wsl --shutdown' then start Docker Desktop manually." -ForegroundColor Red
        return
    }
    
    # Step 4: Fix resolv.conf if broken
    Write-Host "`n[Step 4] Checking DNS config inside docker-desktop..." -ForegroundColor Yellow
    try {
        $dns = wsl -d docker-desktop -- sh -c "cat /etc/resolv.conf 2>&1" 2>&1
        if ($dns -match "No such file" -or $dns -match "can't open" -or [string]::IsNullOrWhiteSpace(($dns -join ""))) {
            Write-Host "  resolv.conf is missing/broken. Fixing..." -ForegroundColor Red
            wsl -d docker-desktop -- sh -c "printf 'nameserver 8.8.8.8\nnameserver 1.1.1.1\n' > /etc/resolv.conf"
            Write-Host "  Created /etc/resolv.conf with public DNS servers" -ForegroundColor Green
        } else {
            Write-Host "  resolv.conf OK" -ForegroundColor Green
        }
    } catch {
        Write-Host "  Could not check DNS (WSL command failed)" -ForegroundColor Gray
    }
    
    # Step 5: Verify docker pull
    Write-Host "`n[Step 5] Verifying Docker Hub connectivity..." -ForegroundColor Yellow
    Start-Sleep -Seconds 5
    if (Test-DockerPull) {
        Write-Host "  Docker pull works!" -ForegroundColor Green
    } else {
        Write-Host "  Docker pull still failing." -ForegroundColor Red
        Write-Host "  Try: wsl --shutdown; then start Docker Desktop manually" -ForegroundColor Yellow
        return
    }
    
    # Step 6: Restart crashing pods
    Invoke-RestartCrashingPods
    
    Write-Host "`n=== Fix complete ===" -ForegroundColor Green
}

# ============ Main ============

Write-Host "Docker Desktop / WSL2 Network Fix" -ForegroundColor Green
Write-Host "==================================" -ForegroundColor Green

switch ($Action) {
    "diagnose"     { Show-Diagnosis }
    "fix"          { Invoke-Fix }
    "restart-pods" { Invoke-RestartCrashingPods }
}
