using Orleans;

namespace ECK1.ReadProjector.Events;

public interface ISampleEvent
{
    Guid SampleId { get; }
    DateTimeOffset OccurredAt { get; set; }
}

[GenerateSerializer]
public record SampleEvent(Guid SampleId) : ISampleEvent
{
    [Id(0)]
    public DateTimeOffset OccurredAt { get; set; }
}

[GenerateSerializer]
public record SampleCreatedEvent(Guid SampleId, string Name, string Description, SampleAddress Address) : SampleEvent(SampleId);
[GenerateSerializer]
public record SampleNameChangedEvent(Guid SampleId, string NewName) : SampleEvent(SampleId);
[GenerateSerializer]
public record SampleDescriptionChangedEvent(Guid SampleId, string NewDescription) : SampleEvent(SampleId);
[GenerateSerializer]
public record SampleAddressChangedEvent(Guid SampleId, SampleAddress NewAddress) : SampleEvent(SampleId);
[GenerateSerializer]
public record SampleAttachmentAddedEvent(Guid SampleId, SampleAttachment Attachment) : SampleEvent(SampleId);
[GenerateSerializer]
public record SampleAttachmentRemovedEvent(Guid SampleId, Guid AttachmentId) : SampleEvent(SampleId);
[GenerateSerializer]
public record SampleAttachmentUpdatedEvent(Guid SampleId, Guid AttachmentId, string NewFileName, string NewUrl) : SampleEvent(SampleId);

[GenerateSerializer]
public record SampleRebuiltEvent(Guid SampleId, string Name, string Description, SampleAddress Address, List<SampleAttachment> attachments) : SampleEvent(SampleId);

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
