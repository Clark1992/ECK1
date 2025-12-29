$ErrorActionPreference = "Stop"
try {
    & 'src\ECK1.CommandsAPI\Deploy\RunLocally.ps1'

    & 'src\ECK1.FailedViewRebuilder\Deploy\RunLocally.ps1'

    & 'src\ECK1.QueriesAPI\Deploy\RunLocally.ps1'

    & 'src\ECK1.ViewProjector\Deploy\RunLocally.ps1'

    & 'src\Integration\IntegrationProxyPlugins\ECK1.Integration.Plugin.ElasticSearch\Deploy\DeployLocally.ps1'

    & 'src\Integration\IntegrationProxyPlugins\ECK1.Integration.Plugin.Clickhouse\Deploy\DeployLocally.ps1'
    
    & 'src\Integration\ECK1.Integration.Cache.ShortTerm\Deploy\RunLocally.ps1'

    & 'src\Integration\ECK1.Integration.Proxy\Deploy\RunLocally.ps1'

} catch {
    Write-Error "Error: $_"
    throw
}
