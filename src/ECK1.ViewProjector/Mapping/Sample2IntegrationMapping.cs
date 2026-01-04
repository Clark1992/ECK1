using AutoMapper;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;
using ECK1.ViewProjector.Views;

namespace ECK1.ViewProjector.Mapping;

public class Sample2IntegrationMapping : Profile
{
    public Sample2IntegrationMapping()
    {
        this.CreateMap<Sample2CustomerView, Sample2Customer>(MemberList.Destination);
        this.CreateMap<Sample2AddressView, Sample2Address>(MemberList.Destination);
        this.CreateMap<Sample2MoneyView, Sample2Money>(MemberList.Destination);
        this.CreateMap<Sample2LineItemView, Sample2LineItem>(MemberList.Destination);

        this.CreateMap<Sample2View, Sample2FullRecord>(MemberList.Destination)
            .ForMember(x => x.Version, o => o.Ignore())
            .ForMember(x => x.OccuredAt, o => o.Ignore())
            .ForMember(x => x.Status, o => o.MapFrom(v => (Sample2Status)v.Status))
            .ForMember(x => x.Tags, o => o.MapFrom(v => v.Tags));

        this.CreateMap<Sample2ThinEvent, Sample2FullRecord>();

        // View has tags as strings; FullRecord uses Sample2Tag wrapper.
        this.CreateMap<string, Sample2Tag>(MemberList.Destination)
            .ForMember(x => x.Value, o => o.MapFrom(s => s));
    }
}
