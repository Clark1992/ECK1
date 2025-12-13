# Specific variables
$pluginProj = "ECK1.Integration.Plugin.ElasticSearch"
$plugin = "ElasticSearch"

& ./src/Integration/IntegrationProxyPlugins/Deploy/DeployPluginLocally.ps1 `
    -pluginProj $pluginProj `
    -plugin $plugin