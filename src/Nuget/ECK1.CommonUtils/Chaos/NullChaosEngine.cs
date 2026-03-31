namespace ECK1.CommonUtils.Chaos;

public sealed class NullChaosEngine : IChaosEngine
{
    public bool IsActive(string scenarioId) => false;
    public void Check(string scenarioId) { }
    public IReadOnlyCollection<string> GetActiveScenarios() => [];
    public void Activate(string scenarioId) { }
    public void Deactivate(string scenarioId) { }
    public void DeactivateAll() { }
}
