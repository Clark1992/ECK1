using AutoMapper;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using ECK1.ViewProjector.Views;

namespace ECK1.ViewProjector.Mapping;

public class SampleIntegrationMapping : Profile
{
    public SampleIntegrationMapping()
    {
        this.CreateMap<SampleAddressView, SampleAddress>(MemberList.Destination);
        this.CreateMap<SampleAttachmentView, SampleAttachment>(MemberList.Destination);
        this.CreateMap<SampleView, SampleFullRecord>(MemberList.Destination)
            .ForMember(x => x.Version, o => o.Ignore())
            .ForMember(x => x.OccuredAt, o => o.Ignore());
        this.CreateMap<SampleThinEvent, SampleFullRecord>();
    }
}
