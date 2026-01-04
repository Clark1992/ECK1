using AutoMapper;
using ECK1.Kafka;
using MediatR;

namespace ECK1.ViewProjector.Handlers;

public interface IId
{
    public Guid EntityId { get; }
}

public class EventFailure : INotification, IId
{
    public Guid EntityId { get; set; }
    public string FailedEventType { get; set; }
    public DateTimeOffset FailureOccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
}

public class SampleEventFailure : EventFailure;
public class Sample2EventFailure : EventFailure;

public class EventFailureHandler<TFailure, TContract>(IMapper mapper,
    IKafkaTopicProducer<TContract> producer) : 
    INotificationHandler<TFailure>
    where TFailure : EventFailure
{
    public Task Handle(TFailure failure, CancellationToken ct) => 
        producer.ProduceAsync(
            (TContract)mapper.Map(failure, typeof(TFailure), typeof(TContract)),
            failure.EntityId.ToString(),
            default);
}
