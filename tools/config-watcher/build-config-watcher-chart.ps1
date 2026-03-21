param(
    [string]$Registry = "localhost:5000"
)

$ErrorActionPreference = "Stop"

$chartDir = $PSScriptRoot
$ociUrl = "oci://$Registry/helm"

Write-Host "Packaging config-watcher chart from $chartDir..."
$output = helm package $chartDir --destination $chartDir
if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm package failed"
    throw
}

$tgzFile = Get-ChildItem -Path $chartDir -Filter "config-watcher-*.tgz" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $tgzFile) {
    Write-Error "Could not find packaged chart .tgz"
    throw
}

Write-Host "Pushing $($tgzFile.Name) to $ociUrl..."
helm push $tgzFile.FullName $ociUrl
if ($LASTEXITCODE -ne 0) {
    Write-Error "Helm push failed"
    throw
}

Remove-Item $tgzFile.FullName -Force
Write-Host "config-watcher chart pushed to $ociUrl"
