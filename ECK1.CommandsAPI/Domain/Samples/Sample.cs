namespace ECK1.CommandsAPI.Domain.Samples;

public class Sample : AggregateRoot<ISampleEvent>
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public SampleAddress Address { get; private set; }

    private readonly List<SampleAttachment> _attachments = new();
    public IReadOnlyCollection<SampleAttachment> Attachments => _attachments.AsReadOnly();

    private Sample() { } // EF

    public static Sample Create(string name, string description, SampleAddress address = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        var sample = new Sample();
        sample.ApplyChange(new SampleCreatedEvent(sample.Id, name, description, address));
        return sample;
    }

    public void ChangeName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        ApplyChange(new SampleNameChangedEvent(Id, name));
    }

    public void ChangeDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        ApplyChange(new SampleDescriptionChangedEvent(Id, description));
    }

    public void ChangeAddress(SampleAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);

        ApplyChange(new SampleAddressChangedEvent(Id, address));
    }

    public void AddAttachment(SampleAttachment attachment)
    {
        if (_attachments.Any(a => a.Id == attachment.Id))
            throw new InvalidOperationException($"Attachment with id {attachment.Id} already exists");

        ApplyChange(new SampleAttachmentAddedEvent(Id, attachment));
    }

    public void RemoveAttachment(Guid attachmentId)
    {
        if (_attachments.All(a => a.Id != attachmentId))
            throw new InvalidOperationException($"Attachment with id {attachmentId} does not exist");

        ApplyChange(new SampleAttachmentRemovedEvent(Id, attachmentId));
    }

    public void UpdateAttachment(Guid attachmentId, string newFileName, string newUrl)
    {
        var attachment = _attachments.FirstOrDefault(a => a.Id == attachmentId)
            ?? throw new InvalidOperationException($"Attachment with Id {attachmentId} not found");

        attachment.ValidateUpdate(newFileName, newUrl);

        ApplyChange(new SampleAttachmentUpdatedEvent(Id, attachmentId, newFileName, newUrl));
    }

    private static void Apply(Sample sample, SampleCreatedEvent @event)
    {
        sample.Id = @event.SampleId;
        sample.Name = @event.Name;
        sample.Description = @event.Description;
        sample.Address = @event.Address;

        sample._attachments.Clear();
    }

    private static void Apply(Sample sample, SampleNameChangedEvent @event)
    {
        sample.Name = @event.NewName;
    }

    private static void Apply(Sample sample, SampleDescriptionChangedEvent @event)
    {
        sample.Description = @event.NewDescription;
    }

    private static void Apply(Sample sample, SampleAddressChangedEvent @event)
    {
        sample.Address = @event.NewAddress;
    }

    private static void Apply(Sample sample, SampleAttachmentAddedEvent @event)
    {
        sample._attachments.Add(@event.Attachment);
    }

    private static void Apply(Sample sample, SampleAttachmentRemovedEvent @event)
    {
        var toRemove = sample._attachments.FirstOrDefault(a => a.Id == @event.AttachmentId);
        if (toRemove != null)
        {
            sample._attachments.Remove(toRemove);
        }
    }

    private static void Apply(Sample sample, SampleAttachmentUpdatedEvent @event)
    {
        var attachment = sample._attachments.First(a => a.Id == @event.AttachmentId);
        attachment.ApplyUpdate(@event.NewFileName, @event.NewUrl);
    }
}
