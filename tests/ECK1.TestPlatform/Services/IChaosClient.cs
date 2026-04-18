namespace ECK1.TestPlatform.Services;

public interface IChaosClient
{
    Task ActivateAsync(string scenarioId, CancellationToken ct);
    Task DeactivateAsync(string scenarioId, CancellationToken ct);
    Task DeactivateAllAsync(CancellationToken ct);
}
