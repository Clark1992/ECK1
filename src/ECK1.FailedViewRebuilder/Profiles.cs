using AutoMapper;
using ECK1.FailedViewRebuilder.Data.Models;

namespace ECK1.FailedViewRebuilder;

public class ModelsProfile: Profile
{
    public ModelsProfile()
    {
        CreateMap<Contracts.Kafka.BusinessEvents.Sample.SampleEventFailure, SampleEventFailure>();
    }
}
