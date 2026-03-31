using ECK1.Contracts.Kafka.BusinessEvents;
using ECK1.FailedViewRebuilder.Data;
using ECK1.Kafka;

namespace ECK1.FailedViewRebuilder.Kafka;

public class EventFailuresHandler(FailuresDbContext db)
    : IKafkaMessageHandler<EventFailure>
{
    public Task Handle(
        string _,
        EventFailure message,
        KafkaMessageId __,
        CancellationToken ct) =>
        UpsertAsync(message.EntityType, message.EntityId, message.FailedEventType, message.FailureOccurredAt, message.ErrorMessage, message.StackTrace, ct);

    private async Task UpsertAsync(
        string entityType,
        Guid entityId,
        string failedEventType,
        DateTimeOffset failureOccurredAt,
        string errorMessage,
        string stackTrace,
        CancellationToken ct)
    {
        var existing = await db.EventFailures.FindAsync([entityType, entityId], cancellationToken: ct);

        if (existing is null)
        {
            db.EventFailures.Add(new Data.Models.EventFailure
            {
                EntityType = entityType,
                EntityId = entityId,
                FailedEventType = failedEventType,
                FailureOccurredAt = failureOccurredAt,
                ErrorMessage = errorMessage,
                StackTrace = stackTrace,
            });
        }
        else
        {
            existing.FailedEventType = failedEventType;
            existing.FailureOccurredAt = failureOccurredAt;
            existing.ErrorMessage = errorMessage;
            existing.StackTrace = stackTrace;
        }

        await db.SaveChangesAsync(ct);
    }
}
