# Specific variables
$pluginProj = "ECK1.Integration.Plugin.Mongo"
$plugin = "Mongo"

& ./src/Integration/IntegrationProxyPlugins/Deploy/DeployPluginLocally.ps1 `
    -pluginProj $pluginProj `
    -plugin $plugin
