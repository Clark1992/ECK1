using AutoMapper;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;
using IntegrationSample2Address = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2.Sample2Address;
using IntegrationSample2Customer = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2.Sample2Customer;
using IntegrationSample2LineItem = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2.Sample2LineItem;
using IntegrationSample2Money = ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2.Sample2Money;

namespace ECK1.CommandsAPI.Mapping;

public class Sample2IntegrationRecordMapping : Profile
{
    public Sample2IntegrationRecordMapping()
    {
        CreateMap<Domain.Sample2s.Sample2Customer, IntegrationSample2Customer>(MemberList.Destination);
        CreateMap<Domain.Shared.Address, IntegrationSample2Address>(MemberList.Destination);
        CreateMap<Domain.Sample2s.Sample2Money, IntegrationSample2Money>(MemberList.Destination);
        CreateMap<Domain.Sample2s.Sample2LineItem, IntegrationSample2LineItem>(MemberList.Destination);
        CreateMap<string, Sample2Tag>(MemberList.Destination)
            .ForMember(x => x.Value, o => o.MapFrom(src => src));
        CreateMap<Sample2, Sample2FullRecord>(MemberList.Destination)
            .ForMember(x => x.OccuredAt, o => o.Ignore())
            .ForMember(x => x.LineItems, o => o.MapFrom(src => src.LineItems.ToList()))
            .ForMember(x => x.Tags, o => o.MapFrom(src => src.Tags.ToList()));
    }
}
