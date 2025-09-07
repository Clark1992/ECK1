namespace ECK1.CommandsAPI.Models;

public class SampleSnapshotEntity
{
    public Guid SnapshotId { get; set; }
    public Guid SampleId { get; set; }
    public int Version { get; set; }
    public string SnapshotData { get; set; } = default!;
    public DateTimeOffset CreatedAt { get; set; }
}
