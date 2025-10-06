using ECK1.CommonUtils.Mapping;
using ECK1.ViewProjector.Events;
using BusinessEvents = ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ViewEvents = ECK1.ViewProjector.Events;

namespace ECK1.ViewProjector.Mapping;

public class SampleMapping: MapByInterface<BusinessEvents.ISampleEvent, ISampleEvent>
{
}
