using ECK1.CommonUtils.Mapping;
using ECK1.ViewProjector.Events;
using ECK1.ViewProjector.Handlers;
using Contract = ECK1.Contracts.Kafka.BusinessEvents.Sample;

namespace ECK1.ViewProjector.Mapping;

public class SampleEventsMapping: MapByInterface<Contract.ISampleEvent, ISampleEvent>
{
    public SampleEventsMapping(): base()
    {
    }
}
