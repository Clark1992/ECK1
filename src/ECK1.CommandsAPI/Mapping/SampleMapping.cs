using BusinessEvents = ECK1.Contracts.BusinessEvents;
using DomainEvents = ECK1.CommandsAPI.Domain.Samples;

namespace ECK1.CommandsAPI.Mapping;

public class SampleMapping: MapByInterface<DomainEvents.ISampleEvent, BusinessEvents.Sample.ISampleEvent>
{
}
