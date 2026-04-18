namespace ECK1.TestPlatform.Scenarios;

public sealed class ScenarioRegistry
{
    private readonly Dictionary<string, IScenario> _scenarios;

    public ScenarioRegistry(IEnumerable<IScenario> scenarios)
    {
        _scenarios = scenarios.ToDictionary(s => s.Definition.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ScenarioDefinition> GetAll() => [.. _scenarios.Values.Select(s => s.Definition)];

    public IScenario? Get(string scenarioId)
        => _scenarios.GetValueOrDefault(scenarioId);
}
