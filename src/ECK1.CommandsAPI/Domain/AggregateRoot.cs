using ECK1.CommonUtils.Handler;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ECK1.CommandsAPI.Domain;

[HandlerMethod(nameof(Apply))]
public abstract class AggregateRoot<TEvent> : GenericHandler<TEvent>
{
    private readonly List<TEvent> _uncommittedEvents = new();

    public Guid Id { get; protected set; } = Guid.NewGuid();
    public int Version { get; protected set; } = 0;

    [JsonIgnore]
    public IReadOnlyCollection<TEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    protected void ApplyChange(TEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    public TAggregate ReplayHistory<TAggregate>(IEnumerable<TEvent> history)
        where TAggregate : AggregateRoot<TEvent>
    {
        foreach (var e in history)
        {
            this.Apply(e);
            this.Version++;
        }

        return this as TAggregate;
    }

    public void MarkEventsAsCommitted() => _uncommittedEvents.Clear();

    public static TAggregate FromHistory<TAggregate>(IEnumerable<TEvent> history)
        where TAggregate : AggregateRoot<TEvent>
    {
        var aggregate = AggregateFactory<TAggregate, TEvent>.Create();
        foreach (var e in history)
        {
            aggregate.Apply(e);
            aggregate.Version++;
        }
        return aggregate;
    }

    protected void Apply(TEvent @event) => Handle(@event);
}

public static class AggregateFactory<TAggregate, TEvent>
    where TAggregate : AggregateRoot<TEvent>
{
    private static readonly Func<TAggregate> _factory;

    static AggregateFactory()
    {
        // Find private parameterless ctor
        var ctor = typeof(TAggregate)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .FirstOrDefault(c => c.GetParameters().Length == 0);

        if (ctor == null)
            throw new InvalidOperationException($"Type {typeof(TAggregate).Name} must have a private parameterless constructor");

        // Build delegate: () => new TAggregate()
        var newExp = Expression.New(ctor);
        var lambda = Expression.Lambda<Func<TAggregate>>(newExp);
        _factory = lambda.Compile();
    }

    public static TAggregate Create() => _factory();
}
