param(
    [string]$Environment = "local"
)

. .github\scripts\common.ps1

$ErrorActionPreference = "Stop"

Write-Host "🔹 Deploying $Environment infrastructure..."

try {
    # 1 - Prepare
    Ensure-Helm
    Ensure-HelmDiff
    Ensure-HelmFile

    # 2 - Deploy Phase 0 (Prepare global config)

    helmfile -f infra\phase-0-helmfile.yaml.gotmpl -e $Environment apply --skip-diff-on-install

    if ($LASTEXITCODE -ne 0) {
        Write-Error "helmfile (Phase 0) deploy failed"
        throw
    }

    . ".github\scripts\prepare.global.vars.ps1"

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Prepare global vars failed"
        throw
    }

    # 3 - Deploy Phase 1 (mostly kafka and other prereqs for further deployment)
    helmfile -f infra\phase-1-helmfile.yaml.gotmpl -e $Environment apply --skip-diff-on-install
    # helmfile -f infra\phase-1-helmfile.yaml.gotmpl -e $Environment build

    if ($LASTEXITCODE -ne 0) {
        Write-Error "helmfile (Phase 1) deploy failed"
        throw
    }

    # 4 - Setup kafka creds for further steps
    # --- Extract credentials ---
    Write-Host "Extracting KafkaUser credentials..."

    $DeployAllPhaseDefaultsYamlPath = "./infra/phase-all-values.default.yaml"
    $KafkaNamespace = Get-YamlValue -YamlPath $DeployAllPhaseDefaultsYamlPath -PropPath "kafka.namespace"
    $KafkaUserSecretName = Get-YamlValue -YamlPath $DeployAllPhaseDefaultsYamlPath -PropPath "kafka.userSecretName"

    $KafkaClusterEnvYamlPath = "./infra/k8s/charts/kafka/cluster/values.$Environment.yaml"
    $K8sHost = Get-YamlValue -YamlPath $KafkaClusterEnvYamlPath -PropPath "k8sHost"
    $externalPort = Get-YamlValue -YamlPath $KafkaClusterEnvYamlPath -PropPath "fixedPorts.bootstrapPort"

    $PasswordB64 = kubectl get secret $KafkaUserSecretName -n $KafkaNamespace -o jsonpath='{.data.password}'
    $Password = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($PasswordB64))

    $jaasConfigB64 = kubectl get secret $KafkaUserSecretName -n $KafkaNamespace -o jsonpath="{.data['sasl\.jaas\.config']}"
    $jaasConfig = [System.Text.Encoding]::UTF8.GetString([System.Convert]::FromBase64String($jaasConfigB64))

    Write-Host "Extracted"

    # --- Set environment variables for subsequent scripts ---
    Write-Host "Setting environment variables for Kafka credentials..."

    $KafkaClusterName = "$Environment-cluster"
    $env:KAFKA_CLUSTER = $KafkaClusterName
    $env:KAFKA_USERNAME = $KafkaUserSecretName
    $env:KAFKA_PASSWORD = $Password
    $env:KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE = "$KafkaClusterName-kafka-bootstrap.$KafkaNamespace.svc.cluster.local:9093"
    $env:KAFKA_BOOTSTRAP_WITH_NAMESPACE = "$KafkaClusterName-kafka-bootstrap.$KafkaNamespace.svc.cluster.local:9092"
    $env:KAFKA_JAAS_CONFIG = $jaasConfig

    $env:KAFKA_EXTERNAL_TLS_BOOTSTRAP = "${K8sHost}:${externalPort}"

    Write-Host "Kafka credentials ready:"
    Write-Host "  KAFKA_CLUSTER=$env:KAFKA_CLUSTER"
    Write-Host "  KAFKA_USERNAME=$env:KAFKA_USERNAME"
    Write-Host "  KAFKA_PASSWORD=<hidden>"
    Write-Host "  KAFKA_BOOTSTRAP_WITH_NAMESPACE=$env:KAFKA_BOOTSTRAP_WITH_NAMESPACE"
    Write-Host "  KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE=$env:KAFKA_TLS_BOOTSTRAP_WITH_NAMESPACE"
    Write-Host "  KAFKA_EXTERNAL_TLS_BOOTSTRAP=$env:KAFKA_EXTERNAL_TLS_BOOTSTRAP"
    Write-Host "  KAFKA_JAAS_CONFIG=<hidden>"

    Write-Host "✅ === Kafka cluster deployed successfully! ==="

    # 5 - Deploy Phase 2

    helmfile -f infra\phase-2-helmfile.yaml.gotmpl -e $Environment apply --skip-diff-on-install

    if ($LASTEXITCODE -ne 0) {
        Write-Error "helmfile deploy failed"
        throw
    }
    # helmfile -f infra\phase-2-helmfile.yaml.gotmpl -e $Environment build

    # 6 - UPDATE Kafka Schemas
    if ($Environment -eq "local") {
        $ingressName = "apicurio-registry-local-dns"
    } else {
        $ingressName = "apicurio-registry"
    }

    # Wait for ingress rule to create
    $ApicurioNamespace = Get-YamlValue -YamlPath $DeployAllPhaseDefaultsYamlPath -PropPath "apicurio.namespace"
    $cmd = "kubectl get ingress $ingressName -n $ApicurioNamespace --ignore-not-found"
    WaitFor-Command -Command $cmd -MaxAttempts 30
    $ingressData = kubectl get ingress $ingressName -n $ApicurioNamespace -o json | ConvertFrom-Json
    $ingressRuleSchemaRegistryHost = $ingressData.spec.rules.host

    # Wait for ingress rule to create
    $ingressControllerName = "ingress-nginx-controller"
    $ingressNS = Get-YamlValue -YamlPath ".\infra\phase-1-values.default.yaml" -PropPath "ingress.namespace"

    $cmd = "kubectl get service $ingressControllerName -n $ingressNS --ignore-not-found"
    WaitFor-Command -Command $cmd -MaxAttempts 30
    $ingressControllerData = kubectl get service $ingressControllerName -n $ingressNS -o json | ConvertFrom-Json
    $ingressHttpPort = ($ingressControllerData.spec.ports | Where-Object { $_.targetPort -eq "http" }).nodePort
    $ingressHttpsPort = ($ingressControllerData.spec.ports | Where-Object { $_.targetPort -eq "https" }).nodePort

    $env:KAFKA_SCHEMAREGISTRY_URL ="http://${ingressRuleSchemaRegistryHost}:$ingressHttpPort/apis/ccompat/v7"

    & .github/scripts/update-kafka-schema.ps1 -SchemaDir ".\infra\k8s\charts\kafka\topic\topic-configs"

} catch {
    Write-Error "Error: $_"
    throw
}