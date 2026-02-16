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
[JsonDerivedType(typeof(SampleRebuiltEvent), nameof(SampleRebuiltEvent))]

public interface ISampleEvent
{
    Guid SampleId { get; }
    Guid EventId { get; }
    DateTimeOffset OccurredAt { get; }
    int Version { get; set; }
}

public abstract class SampleEvent : ISampleEvent
{
    public Guid SampleId { get; set; }
    public Guid EventId { get; set; }
    public int Version { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}

public class SampleCreatedEvent : SampleEvent
{
    public string Name { get; set; }
    public string Description { get; set; }
    public SampleAddress Address { get; set; }
}

public class SampleNameChangedEvent : SampleEvent
{
    public string NewName { get; set; }
}

public class SampleDescriptionChangedEvent : SampleEvent
{
    public string NewDescription { get; set; }
}

public class SampleAddressChangedEvent : SampleEvent
{
    public SampleAddress NewAddress { get; set; }
}

public class SampleAttachmentAddedEvent : SampleEvent
{
    public SampleAttachment Attachment { get; set; }
}

public class SampleAttachmentRemovedEvent : SampleEvent
{
    public Guid AttachmentId { get; set; }
}

public class SampleAttachmentUpdatedEvent : SampleEvent
{
    public Guid AttachmentId { get; set; }
    public string NewFileName { get; set; }
    public string NewUrl { get; set; }
}

public class SampleRebuiltEvent : SampleEvent
{
    public string Name { get; set; }
    public string Description { get; set; }
    public SampleAddress Address { get; set; }
    public List<SampleAttachment> Attachments { get; set; }
}

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
