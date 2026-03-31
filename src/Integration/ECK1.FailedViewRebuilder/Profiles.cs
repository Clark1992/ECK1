using AutoMapper;
using ECK1.Contracts.Kafka.BusinessEvents;

namespace ECK1.FailedViewRebuilder;

public class ModelsProfile: Profile
{
    public ModelsProfile()
    {
        CreateMap<EventFailure, Data.Models.EventFailure>();
    }
}
