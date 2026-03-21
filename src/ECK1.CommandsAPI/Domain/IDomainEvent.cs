namespace ECK1.CommandsAPI.Domain;

public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; set; }
    int Version { get; set; }
}
