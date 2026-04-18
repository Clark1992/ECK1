namespace ECK1.TestPlatform.Scenarios;

public interface IScenario
{
    ScenarioDefinition Definition { get; }
    Task RunAsync(ScenarioRunContext context, CancellationToken ct);
}
