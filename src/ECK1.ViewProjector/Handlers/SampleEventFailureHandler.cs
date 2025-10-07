using AutoMapper;
using ECK1.Kafka;
using MediatR;

namespace ECK1.ViewProjector.Handlers;

public class SampleEventFailure : INotification
{
    public Guid SampleId { get; set; }
    public string FailedEventType { get; set; }
    public DateTimeOffset FailureOccurredAt { get; set; } = DateTimeOffset.UtcNow;
    public string ErrorMessage { get; set; }
    public string StackTrace { get; set; }
}

public class SampleEventFailureHandler(IMapper mapper,
    IKafkaTopicProducer<Contracts.Kafka.BusinessEvents.Sample.SampleEventFailure> producer) : 
    INotificationHandler<SampleEventFailure>
{
    public Task Handle(SampleEventFailure failure, CancellationToken ct) => 
        producer.ProduceAsync(
            mapper.Map<Contracts.Kafka.BusinessEvents.Sample.SampleEventFailure>(failure),
            failure.SampleId.ToString(),
            default);
}
