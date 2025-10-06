namespace ECK1.FailedViewRebuilder.Data.Models;

public class SampleEventFailure
{
    public Guid SampleId { get; set; }

    public string FailedEventType { get; set; }

    public DateTimeOffset FailureOccurredAt { get; set; }

    public string ErrorMessage { get; set; }

    public string StackTrace { get; set; }
}
