namespace ECK1.CommandsAPI.Data.Models;

public class SampleSnapshotEntity : SnapshotEntity;

public class Sample2SnapshotEntity : SnapshotEntity;

public abstract class SnapshotEntity
{
    public Guid SnapshotId { get; set; }
    public Guid AggregateId { get; set; }
    public int Version { get; set; }
    public string SnapshotData { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
