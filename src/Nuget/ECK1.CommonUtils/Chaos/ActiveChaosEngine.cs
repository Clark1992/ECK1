using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECK1.CommonUtils.Chaos;

public sealed class ActiveChaosEngine : IChaosEngine
{
    private readonly ConcurrentDictionary<string, byte> _scenarios = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<ActiveChaosEngine> _logger;

    public ActiveChaosEngine(IOptions<ChaosConfig> options, ILogger<ActiveChaosEngine> logger)
    {
        _logger = logger;

        foreach (var scenario in options.Value.Scenarios)
            _scenarios[scenario] = 0;

        if (_scenarios.Count > 0)
            logger.LogWarning("ChaosEngine activated with scenarios: [{Scenarios}]", string.Join(", ", _scenarios.Keys));
        else
            logger.LogWarning("ChaosEngine activated with no pre-configured scenarios");
    }

    public bool IsActive(string scenarioId) => _scenarios.ContainsKey(scenarioId);

    public void Check(string scenarioId)
    {
        if (!IsActive(scenarioId))
            return;

        _logger.LogWarning("CHAOS: Scenario '{ScenarioId}' triggered", scenarioId);
        throw new ChaosSimulationException(scenarioId);
    }

    public IReadOnlyCollection<string> GetActiveScenarios() => [.. _scenarios.Keys];

    public void Activate(string scenarioId)
    {
        _scenarios[scenarioId] = 0;
        _logger.LogWarning("CHAOS: Scenario '{ScenarioId}' activated", scenarioId);
    }

    public void Deactivate(string scenarioId)
    {
        _scenarios.TryRemove(scenarioId, out _);
        _logger.LogWarning("CHAOS: Scenario '{ScenarioId}' deactivated", scenarioId);
    }

    public void DeactivateAll()
    {
        _scenarios.Clear();
        _logger.LogWarning("CHAOS: All scenarios deactivated");
    }
}
