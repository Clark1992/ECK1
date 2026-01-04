using AutoMapper;
using ECK1.FailedViewRebuilder.Data.Models;

namespace ECK1.FailedViewRebuilder;

public class ModelsProfile: Profile
{
    public ModelsProfile()
    {
        CreateMap<Contracts.Kafka.BusinessEvents.Sample.SampleEventFailure, EventFailure>()
            .ForMember(x => x.EntityType, o => o.MapFrom(_ => EntityType.Sample));
        CreateMap<Contracts.Kafka.BusinessEvents.Sample2.Sample2EventFailure, EventFailure>()
            .ForMember(x => x.EntityType, o => o.MapFrom(_ => EntityType.Sample2));
    }
}
