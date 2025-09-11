using System.Text.Json.Serialization;

namespace ECK1.BusinessEvents.Sample;


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


public class SampleAddress
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Street { get; private set; }
    public string City { get; private set; }
    public string Country { get; private set; }

    private SampleAddress() { } // EF

    public SampleAddress(string street, string city, string country)
    {
        if (string.IsNullOrWhiteSpace(street)) throw new ArgumentException("Street is required");
        if (string.IsNullOrWhiteSpace(city)) throw new ArgumentException("City is required");
        if (string.IsNullOrWhiteSpace(country)) throw new ArgumentException("Country is required");

        Street = street;
        City = city;
        Country = country;
    }
}


public class SampleAttachment
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string FileName { get; private set; } = default!;
    public string Url { get; private set; } = default!;

    public SampleAttachment(Guid id, string fileName, string url)
    {
        Id = id;
        FileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
        Url = url ?? throw new ArgumentNullException(nameof(url));
    }

    public void ValidateUpdate(string newFileName, string newUrl)
    {
        ArgumentNullException.ThrowIfNull(newFileName);
        ArgumentNullException.ThrowIfNull(newUrl);
    }

    public void ApplyUpdate(string newFileName, string newUrl)
    {
        FileName = newFileName;
        Url = newUrl;
    }
}