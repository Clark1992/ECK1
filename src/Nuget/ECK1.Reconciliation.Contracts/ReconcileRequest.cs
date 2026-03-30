namespace ECK1.Reconciliation.Contracts;

public class ReconcileRequest
{
    public List<ReconcileRequestItem> Items { get; set; } = [];
}

public class ReconcileRequestItem
{
    public Guid EntityId { get; set; }
    public string EntityType { get; set; } = "";
    public int ExpectedVersion { get; set; }
}
