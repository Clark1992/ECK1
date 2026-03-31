namespace ECK1.CommonUtils.Chaos;

public interface IChaosEngine
{
    bool IsActive(string scenarioId);
    void Check(string scenarioId);
    IReadOnlyCollection<string> GetActiveScenarios();
    void Activate(string scenarioId);
    void Deactivate(string scenarioId);
    void DeactivateAll();
}
