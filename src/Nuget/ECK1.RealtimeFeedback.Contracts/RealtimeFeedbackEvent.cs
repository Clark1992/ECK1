namespace ECK1.RealtimeFeedback.Contracts;

public class RealtimeFeedbackEvent
{
    public string CorrelationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public bool Success { get; set; } = true;
    public string OutcomeCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Payload { get; set; }
    public int Version { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}
