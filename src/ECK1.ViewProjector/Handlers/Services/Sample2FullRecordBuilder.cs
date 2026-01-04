using AutoMapper;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;
using ECK1.ViewProjector.Data;
using ECK1.ViewProjector.Views;

namespace ECK1.ViewProjector.Handlers.Services;

public class Sample2FullRecordBuilder(IMapper mapper, MongoDbContext context) : IFullRecordBuilder<Sample2View, Sample2ThinEvent, Sample2FullRecord>
{
    public Task<Sample2FullRecord> BuildRecord(Sample2View state, Sample2ThinEvent @event)
    {
        var record = mapper.Map<Sample2FullRecord>(state);
        record = mapper.Map(@event, record);

        // TODO: some async enrichment from context
        return Task.FromResult(record);
    }
}
