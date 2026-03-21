using AutoMapper;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using IntegrationSampleAddress = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample.SampleAddress;
using IntegrationSampleAttachment = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample.SampleAttachment;

namespace ECK1.CommandsAPI.Mapping;

public class SampleIntegrationRecordMapping : Profile
{
    public SampleIntegrationRecordMapping()
    {
        CreateMap<Domain.Shared.Address, IntegrationSampleAddress>(MemberList.Destination);
        CreateMap<ECK1.CommandsAPI.Domain.Samples.SampleAttachment, IntegrationSampleAttachment>(MemberList.Destination);
        CreateMap<Sample, SampleFullRecord>(MemberList.Destination)
            .ForMember(x => x.OccuredAt, o => o.Ignore())
            .ForMember(x => x.Attachments, o => o.MapFrom(src => src.Attachments.ToList()));
    }
}
