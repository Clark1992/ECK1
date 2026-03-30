namespace ECK1.Reconciler.Data.Models;

public class ReconcileFailure
{
    public int Id { get; set; }
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = "";
    public string FailedPlugin { get; set; } = "";
    public bool IsFullHistoryRebuild { get; set; }
    public DateTime FailedAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
}
