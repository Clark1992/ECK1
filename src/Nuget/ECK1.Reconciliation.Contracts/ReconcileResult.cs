namespace ECK1.Reconciliation.Contracts;

public class ReconcileResult
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = "";
    public string FailedPlugin { get; set; } = "";
    public bool IsFullHistoryRebuild { get; set; }
}
