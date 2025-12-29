using ECK1.IntegrationContracts.Abstractions;
using ProtoBuf;

namespace ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;

[ProtoContract]
public class SampleFullRecord: IIntegrationMessage
{
    [ProtoIgnore] public string Id => SampleId.ToString();
    [ProtoMember(1)] public Guid SampleId { get; set; }
    [ProtoMember(2)] public string Name { get; set; }
    [ProtoMember(3)] public string Description { get; set; }
    [ProtoMember(4)] public SampleAddress Address { get; set; }
    [ProtoMember(5)] public List<SampleAttachment> Attachments { get; set; }
    [ProtoMember(6)] public int Version { get; set; }
    [ProtoMember(7)] public DateTime OccuredAt { get; set; }
}

[ProtoContract]
public class SampleAddress
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string Street { get; set; }
    [ProtoMember(3)] public string City { get; set; }
    [ProtoMember(4)] public string Country { get; set; }
}

[ProtoContract]
public class SampleAttachment
{
    [ProtoMember(1)] public Guid Id { get; set; }
    [ProtoMember(2)] public string FileName { get; set; }
    [ProtoMember(3)] public string Url { get; set; }
}
