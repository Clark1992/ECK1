# param(
#     [string]$Namespace = "ingress-nginx",
#     [string]$Environment = 'local',
#     [string]$IngressChartVersion = '4.13.3'
# )

# Write-Host "🔹 Deploying Ingress NGINX to namespace '$Namespace'..."

# helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
# helm repo update

# # if (-not (kubectl get ns $Namespace -o name 2>$null)) {
# #     Write-Host "Namespace '$Namespace' not found, creating..."
# #     kubectl create namespace $Namespace
# # } else {
# #     Write-Host "Namespace '$Namespace' already exists."
# # }

# helm upgrade --install ingress-nginx "ingress-nginx/ingress-nginx" `
#   --namespace $Namespace `
#   --create-namespace
#   --version $IngressChartVersion `
#   -f "./infra/k8s/charts/ingress-nginx/values.${Environment}.yaml" `
#   --set controller.service.nodePorts.http=$env:INGRESS_HTTP_PORT `
#   --set controller.service.nodePorts.https=$env:INGRESS_HTTPS_PORT

# if ($LASTEXITCODE -eq 0) {
#     Write-Host "✅ Ingress NGINX deployed successfully!"
# } else {
#   throw
# }
