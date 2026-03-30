namespace ECK1.Reconciliation.Contracts;

public class RebuildRequest
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = "";
    public string[] FailedTargets { get; set; } = [];
    public bool IsFullHistoryRebuild { get; set; }
}
