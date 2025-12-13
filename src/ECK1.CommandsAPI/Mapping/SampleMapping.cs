using AutoMapper;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommonUtils.Mapping;
using ContractEvents = ECK1.Contracts.Kafka.BusinessEvents;

namespace ECK1.CommandsAPI.Mapping;

public class SampleMapping: MapByInterface<ISampleEvent, ContractEvents.Sample.ISampleEvent>
{
    public SampleMapping(): base()
    {
        // domain entity -> domain event (which will be then mapped to contract event by interface)
        this.CreateMap<Sample, SampleRebuiltEvent>(MemberList.Destination)
            .ForMember(s => s.OccurredAt, o => o.Ignore());
    }
}
