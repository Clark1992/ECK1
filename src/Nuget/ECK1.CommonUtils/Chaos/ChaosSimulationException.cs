namespace ECK1.CommonUtils.Chaos;

public class ChaosSimulationException(string scenarioId)
    : Exception($"Chaos simulation triggered: {scenarioId}")
{
    public string ScenarioId { get; } = scenarioId;
}
