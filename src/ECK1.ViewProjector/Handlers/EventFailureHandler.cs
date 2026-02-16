using AutoMapper;
using ECK1.Kafka;
using MediatR;

using FailureContract = ECK1.Contracts.Kafka.BusinessEvents.EventFailure;

namespace ECK1.ViewProjector.Handlers;

public class EventFailure : INotification
{
    public Guid EntityId { get; set; }
    public string FailedEventType { get; set; }
    public string EntityType { get; set; }
    public DateTimeOffset FailureOccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
}

public class EventFailureHandler(IMapper mapper,
    IKafkaTopicProducer<FailureContract> producer) : 
    INotificationHandler<EventFailure>
{
    public Task Handle(EventFailure failure, CancellationToken ct) => 
        producer.ProduceAsync(
            mapper.Map<FailureContract>(failure),
            failure.EntityId.ToString(),
            default);
}
