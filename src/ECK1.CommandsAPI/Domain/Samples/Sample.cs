namespace ECK1.CommandsAPI.Domain.Samples;

public class Sample : AggregateRoot<ISampleEvent>
{
    public Guid SampleId => Id;
    public string Name { get; private set; } = default;
    public string Description { get; private set; } = default;
    public SampleAddress Address { get; private set; }

    private readonly List<SampleAttachment> _attachments = new();
    public IReadOnlyCollection<SampleAttachment> Attachments => _attachments.AsReadOnly();

    private Sample() { }

    public static Sample Create(string name, string description, SampleAddress address = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrEmpty(description);

        var sample = new Sample();
        sample.ApplyChange(new SampleCreatedEvent(sample.Id, name, description, address));
        return sample;
    }

    public void ChangeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        ApplyChange(new SampleNameChangedEvent(Id, name));
    }

    public void ChangeDescription(string description)
    {
        ArgumentException.ThrowIfNullOrEmpty(description);

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

    private void Apply(SampleCreatedEvent @event)
    {
        this.Id = @event.SampleId;
        this.Name = @event.Name;
        this.Description = @event.Description;
        this.Address = @event.Address;

        this._attachments.Clear();
    }

    private void Apply(SampleNameChangedEvent @event)
    {
        this.Name = @event.NewName;
    }

    private void Apply(SampleDescriptionChangedEvent @event)
    {
        this.Description = @event.NewDescription;
    }

    private void Apply(SampleAddressChangedEvent @event)
    {
        this.Address = @event.NewAddress;
    }

    private void Apply(SampleAttachmentAddedEvent @event)
    {
        this._attachments.Add(@event.Attachment);
    }

    private void Apply(SampleAttachmentRemovedEvent @event)
    {
        var toRemove = this._attachments.FirstOrDefault(a => a.Id == @event.AttachmentId);
        if (toRemove != null)
        {
            this._attachments.Remove(toRemove);
        }
    }

    private void Apply(SampleAttachmentUpdatedEvent @event)
    {
        var attachment = this._attachments.First(a => a.Id == @event.AttachmentId);
        attachment.ApplyUpdate(@event.NewFileName, @event.NewUrl);
    }

    private void Apply(SampleRebuiltEvent @event) { }
}
