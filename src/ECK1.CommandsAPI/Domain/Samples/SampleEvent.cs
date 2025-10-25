using System.Text.Json.Serialization;

namespace ECK1.CommandsAPI.Domain.Samples;

// Required for serializing to DB
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(SampleCreatedEvent), nameof(SampleCreatedEvent))]
[JsonDerivedType(typeof(SampleNameChangedEvent), nameof(SampleNameChangedEvent))]
[JsonDerivedType(typeof(SampleDescriptionChangedEvent), nameof(SampleDescriptionChangedEvent))]
[JsonDerivedType(typeof(SampleAddressChangedEvent), nameof(SampleAddressChangedEvent))]
[JsonDerivedType(typeof(SampleAttachmentAddedEvent), nameof(SampleAttachmentAddedEvent))]
[JsonDerivedType(typeof(SampleAttachmentRemovedEvent), nameof(SampleAttachmentRemovedEvent))]
[JsonDerivedType(typeof(SampleAttachmentUpdatedEvent), nameof(SampleAttachmentUpdatedEvent))]
public interface ISampleEvent
{
    Guid SampleId { get; }
    DateTimeOffset OccurredAt { get; set; }
}

public record SampleEvent(Guid SampleId) : ISampleEvent
{
    [JsonIgnore]
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}

public record SampleCreatedEvent(Guid SampleId, string Name, string Description, SampleAddress Address) : SampleEvent(SampleId);
public record SampleNameChangedEvent(Guid SampleId, string NewName) : SampleEvent(SampleId);
public record SampleDescriptionChangedEvent(Guid SampleId, string NewDescription) : SampleEvent(SampleId);
public record SampleAddressChangedEvent(Guid SampleId, SampleAddress NewAddress) : SampleEvent(SampleId);
public record SampleAttachmentAddedEvent(Guid SampleId, SampleAttachment Attachment) : SampleEvent(SampleId);
public record SampleAttachmentRemovedEvent(Guid SampleId, Guid AttachmentId) : SampleEvent(SampleId);
public record SampleAttachmentUpdatedEvent(Guid SampleId, Guid AttachmentId, string NewFileName, string NewUrl) : SampleEvent(SampleId);
public record SampleRebuiltEvent(Guid SampleId, string Name, string Description, SampleAddress Address, List<SampleAttachment> Attachments) : SampleEvent(SampleId);