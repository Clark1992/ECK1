namespace ECK1.Contracts.Kafka.BusinessEvents;

public class EventFailure
{
    public Guid EntityId { get; set; }
    public string FailedEventType { get; set; }
    public string EntityType { get; set; }
    public DateTimeOffset FailureOccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
}
