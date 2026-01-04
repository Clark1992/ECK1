using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;
using ECK1.Kafka;
using ECK1.ViewProjector.Handlers.Services;
using ECK1.ViewProjector.Notifications;
using ECK1.ViewProjector.Views;
using MediatR;

namespace ECK1.ViewProjector.Handlers;

public class Sample2EventIntegrationHandlers(
    IKafkaTopicProducer<Sample2ThinEvent> thinEventProducer,
    IKafkaTopicProducer<Sample2FullRecord> fullRecordProducer,
    IFullRecordBuilder<Sample2View, Sample2ThinEvent, Sample2FullRecord> fullRecordBuilder) :
    INotificationHandler<EventNotification<Sample2ThinEvent, Sample2View>>
{
    public async Task Handle(EventNotification<Sample2ThinEvent, Sample2View> data, CancellationToken ct)
    {
        var fullRecord = await fullRecordBuilder.BuildRecord(data.State, data.Event);
        var id = fullRecord.Sample2Id.ToString();
        await fullRecordProducer.ProduceAsync(fullRecord, id, default);
        await thinEventProducer.ProduceAsync(data.Event, id, default);
    }
}
