Write-Host "Preparing deployment context"

# INGRESS
$env:INGRESS_HTTP_PORT = 30200
$env:INGRESS_HTTPS_PORT = 30443

$env:SCHEMAREGISTRYURL_INTERNAL = "http://apicurio-registry-service.apicurio.svc.cluster.local:8080/apis/ccompat/v7"