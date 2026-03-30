namespace ECK1.Reconciler.Data.Models;

public class EntityState
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = "";
    public int ExpectedVersion { get; set; }
    public DateTime LastEventOccuredAt { get; set; }
    public DateTime? ReconciledAt { get; set; }
}
