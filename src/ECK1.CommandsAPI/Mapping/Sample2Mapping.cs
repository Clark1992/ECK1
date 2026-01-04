using AutoMapper;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommonUtils.Mapping;
using ContractEvents = ECK1.Contracts.Kafka.BusinessEvents;

namespace ECK1.CommandsAPI.Mapping;

public class Sample2Mapping : MapByInterface<ISample2Event, ContractEvents.Sample2.ISample2Event>
{
    public Sample2Mapping() : base()
    {
        this.CreateMap<Sample2, Sample2RebuiltEvent>(MemberList.Destination)
            .ForMember(s => s.OccurredAt, o => o.Ignore())
            .ForMember(s => s.EventId, o => o.Ignore())
            .ForMember(s => s.Sample2Id, o => o.MapFrom(src => src.Id))
            .ForMember(s => s.LineItems, o => o.MapFrom(src => src.LineItems.ToList()))
            .ForMember(s => s.Tags, o => o.MapFrom(src => src.Tags.ToList()));
    }
}
