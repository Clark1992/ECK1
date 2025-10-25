using AutoMapper;
using ECK1.ViewProjector.Events;
using ECK1.ViewProjector.Views;

namespace ECK1.ViewProjector.Mapping;

public class SampleMapping: Profile
{
    public SampleMapping()
    {
        this.CreateMap<SampleAddress, SampleAddressView>(MemberList.Destination);
        this.CreateMap<SampleAttachment, SampleAttachmentView>(MemberList.Destination);
        this.CreateMap<SampleCreatedEvent, SampleView>(MemberList.Destination)
            .ForMember(s => s.Id, o => o.Ignore())
            .ForMember(s => s.Attachments, o => o.Ignore());
        this.CreateMap<SampleRebuiltEvent, SampleView>(MemberList.Destination)
            .ForMember(s => s.Id, o => o.Ignore());
    }
}
