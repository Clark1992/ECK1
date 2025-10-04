using ECK1.CommonUtils.Mapping;
using BusinessEvents = ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ViewEvents = ECK1.ReadProjector.Events;

namespace ECK1.ReadProjector.Mapping;

public class SampleMapping: MapByInterface<BusinessEvents.ISampleEvent, ViewEvents.ISampleEvent>
{
}
