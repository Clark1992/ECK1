using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommonUtils.Mapping;
using BusinessEvents = ECK1.Contracts.Kafka.BusinessEvents;

namespace ECK1.CommandsAPI.Mapping;

public class SampleMapping: MapByInterface<ISampleEvent, BusinessEvents.Sample.ISampleEvent>
{
    public SampleMapping(): base()
    {
        this.CreateMap<Sample, SampleRebuiltEvent>();
    }
}
