using AutoMapper;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using ECK1.ViewProjector.Data;
using ECK1.ViewProjector.Views;

namespace ECK1.ViewProjector.Handlers.Services;

public class SampleFullRecordBuilder(IMapper mapper, MongoDbContext context) : IFullRecordBuilder<SampleView, SampleThinEvent, SampleFullRecord>
{
    public async Task<SampleFullRecord> BuildRecord(SampleView state, SampleThinEvent @event)
    {
        var record = mapper.Map<SampleFullRecord>(state);
        record = mapper.Map(@event, record);

        // TODO: some async enrichment from context;
        // Or mb think about make enriching data a part of state and leave this sync and small

        return record;
    }
}
