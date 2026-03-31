namespace ECK1.FailedViewRebuilder.Data.Models;

public class EventFailure
{
    public string EntityType { get; set; }

    public Guid EntityId { get; set; }

    public string FailedEventType { get; set; }

    public DateTimeOffset FailureOccurredAt { get; set; }

    public string ErrorMessage { get; set; }

    public string StackTrace { get; set; }
}
