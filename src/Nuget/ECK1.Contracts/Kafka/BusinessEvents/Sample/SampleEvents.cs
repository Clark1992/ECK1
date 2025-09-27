using System;
using System.Text.Json.Serialization;
using ECK1.Contracts.Shared;

namespace ECK1.Contracts.Kafka.BusinessEvents.Sample;

[Newtonsoft.Json.JsonConverter(typeof(Polymorph<ISampleEvent>), "$type")]
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
    public DateTimeOffset OccurredAt { get; set; }
}

public record SampleCreatedEvent(Guid SampleId, string Name, string Description, SampleAddress Address) : SampleEvent(SampleId);
public record SampleNameChangedEvent(Guid SampleId, string NewName) : SampleEvent(SampleId);
public record SampleDescriptionChangedEvent(Guid SampleId, string NewDescription) : SampleEvent(SampleId);
public record SampleAddressChangedEvent(Guid SampleId, SampleAddress NewAddress) : SampleEvent(SampleId);
public record SampleAttachmentAddedEvent(Guid SampleId, SampleAttachment Attachment) : SampleEvent(SampleId);
public record SampleAttachmentRemovedEvent(Guid SampleId, Guid AttachmentId) : SampleEvent(SampleId);
public record SampleAttachmentUpdatedEvent(Guid SampleId, Guid AttachmentId, string NewFileName, string NewUrl) : SampleEvent(SampleId);

public class SampleAddress
{
    public Guid Id { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
}

public class SampleAttachment
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string Url { get; set; }
}
