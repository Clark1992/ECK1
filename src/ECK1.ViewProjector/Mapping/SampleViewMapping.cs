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
        this.CreateMap<SampleCreatedEvent, SampleView>(MemberList.Destination);
        this.CreateMap<SampleRebuiltEvent, SampleView>(MemberList.Destination);
    }
}
