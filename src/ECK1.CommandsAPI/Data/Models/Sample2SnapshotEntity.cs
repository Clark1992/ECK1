namespace ECK1.CommandsAPI.Data.Models;

public class Sample2SnapshotEntity
{
    public Guid SnapshotId { get; set; }
    public Guid Sample2Id { get; set; }
    public int Version { get; set; }
    public string SnapshotData { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
