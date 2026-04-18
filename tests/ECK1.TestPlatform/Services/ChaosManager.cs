using Microsoft.Extensions.Options;

namespace ECK1.TestPlatform.Services;

public sealed class ChaosManager
{
    private readonly IServiceProvider _sp;
    private readonly IReadOnlyList<string> _pluginNames;

    public ChaosManager(IServiceProvider sp, IOptions<ProxyServiceConfig> proxyConfig)
    {
        _sp = sp;
        _pluginNames = [.. proxyConfig.Value.Plugins.Keys];
    }

    public IReadOnlyList<string> AllPluginNames => _pluginNames;

    public IChaosClient GetClient(string key) =>
        _sp.GetRequiredKeyedService<IChaosClient>(key);

    public async Task ActivateOnAsync(IEnumerable<string> targets, string scenarioId, CancellationToken ct)
    {
        foreach (var target in targets)
            await GetClient(target).ActivateAsync(scenarioId, ct);
    }

    public async Task DeactivateOnAsync(IEnumerable<string> targets, string scenarioId, CancellationToken ct)
    {
        foreach (var target in targets)
            await GetClient(target).DeactivateAsync(scenarioId, ct);
    }

    public async Task DeactivateAllOnAsync(IEnumerable<string> targets, CancellationToken ct)
    {
        foreach (var target in targets)
            await GetClient(target).DeactivateAllAsync(ct);
    }
}
