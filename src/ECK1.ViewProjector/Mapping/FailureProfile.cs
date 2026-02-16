using AutoMapper;
using ECK1.ViewProjector.Handlers;

namespace ECK1.ViewProjector.Mapping;

public class FailureProfile : Profile
{
    public FailureProfile()
    {
        CreateMap<EventFailure, Contracts.Kafka.BusinessEvents.EventFailure>();
    }
}
