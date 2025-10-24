param(
    [string]$Environment = "local"
)

. .github\scripts\Common.ps1

$ErrorActionPreference = "Stop"

Write-Host "🔹 Deploying $Environment infrastructure..."

try {
    Ensure-Helm
    Ensure-HelmDiff
    Ensure-HelmFile

    & ${PSScriptRoot}\Deploy.prepare.context.ps1

    helmfile -f infra\kafka-helmfile.yaml.gotmpl -e $Environment apply --skip-diff-on-install
    # helmfile -f infra\kafka-helmfile.yaml.gotmpl -e $Environment build

    # --- Extract credentials ---
    Write-Host "Extracting KafkaUser credentials..."

    $KafkaDefaultsYamlPath = "./infra/kafka-values.default.yaml"
    $KafkaNamespace = Get-YamlValue -YamlPath $KafkaDefaultsYamlPath -PropPath "namespace"
    $KafkaUserSecretName = Get-YamlValue -YamlPath $KafkaDefaultsYamlPath -PropPath "kafkaUserSecretName"

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

    $env:KAFKA_NS = $KafkaNamespace
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

    # APICURIO PREP

    $env:APICURIO_NS = 'apicurio'

    helmfile -f infra\helmfile.yaml.gotmpl -e $Environment apply --skip-diff-on-install
    # helmfile -f infra\helmfile.yaml.gotmpl -e $Environment build

} catch {
    Write-Error "Error: $_"
    throw
}