using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using ECK1.Kafka;
using ECK1.ViewProjector.Handlers.Services;
using ECK1.ViewProjector.Notifications;
using ECK1.ViewProjector.Views;
using MediatR;

namespace ECK1.ViewProjector.Handlers;

public class SampleEventIntegrationHandlers(
    IKafkaTopicProducer<SampleThinEvent> thinEventProducer,
    IKafkaTopicProducer<SampleFullRecord> fullRecordProducer,
    IFullRecordBuilder<SampleView, SampleThinEvent, SampleFullRecord> fullRecordBuilder) : 
    INotificationHandler<EventNotification<SampleThinEvent, SampleView>>
{
    public async Task Handle(EventNotification<SampleThinEvent, SampleView> data, CancellationToken ct)
    {
        var fullRecord = await fullRecordBuilder.BuildRecord(data.State, data.Event);
        var id = fullRecord.SampleId.ToString();
        await fullRecordProducer.ProduceAsync(fullRecord, id, default);
        await thinEventProducer.ProduceAsync(data.Event, id, default);
    }
}
