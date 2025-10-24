# $ErrorActionPreference = "Stop"
# try {
# . ${PSScriptRoot}\Deploy.ps1 -Environment "local"

# & ${PSScriptRoot}\Local.SetupSecrets.ps1

# } catch {
#     Write-Error "Error: $_"
#     throw
# }