using Orleans;

namespace ECK1.ViewProjector.Events;

public interface ISampleEvent
{
    Guid SampleId { get; }
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    int Version { get; }
}

[GenerateSerializer]
public record SampleEvent(Guid SampleId, int Version) : ISampleEvent
{
    [Id(0)]
    public DateTimeOffset OccurredAt { get; set; }

    [Id(1)]
    public Guid EventId { get; set; }
}

[GenerateSerializer]
public record SampleCreatedEvent(Guid SampleId, string Name, string Description, SampleAddress Address, int Version) : SampleEvent(SampleId, Version);
[GenerateSerializer]
public record SampleNameChangedEvent(Guid SampleId, string NewName, int Version) : SampleEvent(SampleId, Version);
[GenerateSerializer]
public record SampleDescriptionChangedEvent(Guid SampleId, string NewDescription, int Version) : SampleEvent(SampleId, Version);
[GenerateSerializer]
public record SampleAddressChangedEvent(Guid SampleId, SampleAddress NewAddress, int Version) : SampleEvent(SampleId, Version);
[GenerateSerializer]
public record SampleAttachmentAddedEvent(Guid SampleId, SampleAttachment Attachment, int Version) : SampleEvent(SampleId, Version);
[GenerateSerializer]
public record SampleAttachmentRemovedEvent(Guid SampleId, Guid AttachmentId, int Version) : SampleEvent(SampleId, Version);
[GenerateSerializer]
public record SampleAttachmentUpdatedEvent(Guid SampleId, Guid AttachmentId, string NewFileName, string NewUrl, int Version) : SampleEvent(SampleId, Version);

[GenerateSerializer]
public record SampleRebuiltEvent(Guid SampleId, string Name, string Description, SampleAddress Address, List<SampleAttachment> attachments, int Version) : SampleEvent(SampleId, Version);

[GenerateSerializer]
public class SampleAddress
{
    [Id(0)]
    public Guid Id { get; set; }
    [Id(1)]
    public string Street { get; set; }
    [Id(2)]
    public string City { get; set; }
    [Id(3)]
    public string Country { get; set; }
}

[GenerateSerializer]
public class SampleAttachment
{
    [Id(0)]
    public Guid Id { get; set; }
    [Id(1)]
    public string FileName { get; set; }
    [Id(2)]
    public string Url { get; set; }
}
