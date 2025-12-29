# Specific variables
$pluginProj = "ECK1.Integration.Plugin.Clickhouse"
$plugin = "Clickhouse"

& ./src/Integration/IntegrationProxyPlugins/Deploy/DeployPluginLocally.ps1 `
    -pluginProj $pluginProj `
    -plugin $plugin