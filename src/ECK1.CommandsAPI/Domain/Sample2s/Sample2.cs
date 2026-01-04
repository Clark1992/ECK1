namespace ECK1.CommandsAPI.Domain.Sample2s;

public class Sample2 : AggregateRoot<ISample2Event>
{
    public Guid Sample2Id => Id;

    public Sample2Customer Customer { get; private set; } = default!;
    public Sample2Address ShippingAddress { get; private set; } = default!;
    public Sample2Status Status { get; private set; }

    private readonly List<Sample2LineItem> _lineItems = new();
    public IReadOnlyCollection<Sample2LineItem> LineItems => _lineItems.AsReadOnly();

    private readonly HashSet<string> _tags = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> Tags => _tags.ToList().AsReadOnly();

    private Sample2() { }

    public static Sample2 Create(
        Sample2Customer customer,
        Sample2Address shippingAddress,
        List<Sample2LineItem> lineItems,
        List<string> tags)
    {
        ArgumentNullException.ThrowIfNull(customer);
        ArgumentNullException.ThrowIfNull(shippingAddress);

        lineItems ??= [];
        tags ??= [];

        var aggregate = new Sample2();

        aggregate.ApplyChange(new Sample2CreatedEvent(
            aggregate.Id,
            customer,
            shippingAddress,
            lineItems,
            tags,
            Sample2Status.Draft));

        return aggregate;
    }

    public void ChangeCustomerEmail(string newEmail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newEmail);
        ApplyChange(new Sample2CustomerEmailChangedEvent(Id, newEmail));
    }

    public void ChangeShippingAddress(Sample2Address newAddress)
    {
        ArgumentNullException.ThrowIfNull(newAddress);
        ApplyChange(new Sample2ShippingAddressChangedEvent(Id, newAddress));
    }

    public void AddLineItem(Sample2LineItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_lineItems.Any(i => i.ItemId == item.ItemId))
            throw new InvalidOperationException($"Line item with id {item.ItemId} already exists");

        ApplyChange(new Sample2LineItemAddedEvent(Id, item));
    }

    public void RemoveLineItem(Guid itemId)
    {
        if (_lineItems.All(i => i.ItemId != itemId))
            throw new InvalidOperationException($"Line item with id {itemId} does not exist");

        ApplyChange(new Sample2LineItemRemovedEvent(Id, itemId));
    }

    public void ChangeStatus(Sample2Status newStatus, string reason)
    {
        ApplyChange(new Sample2StatusChangedEvent(Id, newStatus, reason));
    }

    public void AddTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        if (_tags.Contains(tag)) return;
        ApplyChange(new Sample2TagAddedEvent(Id, tag));
    }

    public void RemoveTag(string tag)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tag);
        if (!_tags.Contains(tag)) return;
        ApplyChange(new Sample2TagRemovedEvent(Id, tag));
    }

    private void Apply(Sample2CreatedEvent @event)
    {
        Id = @event.Sample2Id;
        Customer = @event.Customer;
        ShippingAddress = @event.ShippingAddress;
        Status = @event.Status;

        _lineItems.Clear();
        if (@event.LineItems is not null)
            _lineItems.AddRange(@event.LineItems);

        _tags.Clear();
        if (@event.Tags is not null)
        {
            foreach (var t in @event.Tags)
            {
                if (!string.IsNullOrWhiteSpace(t))
                    _tags.Add(t);
            }
        }
    }

    private void Apply(Sample2CustomerEmailChangedEvent @event)
    {
        Customer.Email = @event.NewEmail;
    }

    private void Apply(Sample2ShippingAddressChangedEvent @event)
    {
        ShippingAddress = @event.NewAddress;
    }

    private void Apply(Sample2LineItemAddedEvent @event)
    {
        _lineItems.Add(@event.Item);
    }

    private void Apply(Sample2LineItemRemovedEvent @event)
    {
        var existing = _lineItems.FirstOrDefault(i => i.ItemId == @event.ItemId);
        if (existing is not null)
            _lineItems.Remove(existing);
    }

    private void Apply(Sample2StatusChangedEvent @event)
    {
        Status = @event.NewStatus;
    }

    private void Apply(Sample2TagAddedEvent @event)
    {
        if (!string.IsNullOrWhiteSpace(@event.Tag))
            _tags.Add(@event.Tag);
    }

    private void Apply(Sample2TagRemovedEvent @event)
    {
        if (!string.IsNullOrWhiteSpace(@event.Tag))
            _tags.Remove(@event.Tag);
    }

    private void Apply(Sample2RebuiltEvent @event) { }
}
