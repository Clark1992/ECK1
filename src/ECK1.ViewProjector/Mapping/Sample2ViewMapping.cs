using AutoMapper;
using ECK1.ViewProjector.Events;
using ECK1.ViewProjector.Views;

namespace ECK1.ViewProjector.Mapping;

public class Sample2Mapping : Profile
{
    public Sample2Mapping()
    {
        this.CreateMap<Sample2Customer, Sample2CustomerView>(MemberList.Destination);
        this.CreateMap<Sample2Address, Sample2AddressView>(MemberList.Destination);
        this.CreateMap<Sample2Money, Sample2MoneyView>(MemberList.Destination);
        this.CreateMap<Sample2LineItem, Sample2LineItemView>(MemberList.Destination);

        this.CreateMap<Sample2CreatedEvent, Sample2View>(MemberList.Destination)
            .ForMember(s => s.Id, o => o.Ignore());

        this.CreateMap<Sample2RebuiltEvent, Sample2View>(MemberList.Destination)
            .ForMember(s => s.Id, o => o.Ignore());
    }
}
