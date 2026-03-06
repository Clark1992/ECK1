using ECK1.CommonUtils.Handler;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ECK1.CommandsAPI.Domain;

public interface IAggregateRoot
{
    Guid Id { get; }
    int Version { get; }
    IReadOnlyCollection<IDomainEvent> UncommittedEvents { get; }
    void CommitEvents(int version);
}

internal interface IAggregateRootInternal
{
    void SetId(Guid id);
    void IncrementVersion();
    void Apply(IDomainEvent @event);
}

[HandlerMethod(nameof(Apply))]
public abstract class AggregateRoot<TEvent> : GenericHandler<TEvent>, IAggregateRoot, IAggregateRootInternal
    where TEvent : class, IDomainEvent
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public int Version { get; protected set; } = 0;

    [JsonIgnore]
    public IReadOnlyCollection<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    protected void ApplyChange(TEvent @event)
    {
        Apply(@event);
        _uncommittedEvents.Add(@event);
    }

    public void CommitEvents(int version)
    {
        _uncommittedEvents.Clear();
        Version = version;
    }

    void IAggregateRootInternal.SetId(Guid id) => Id = id;

    void IAggregateRootInternal.IncrementVersion() => Version++;

    void IAggregateRootInternal.Apply(IDomainEvent @event) => Handle(@event as TEvent);

    void Apply(TEvent @event) => Handle(@event);
}

public static class AggregateRoot
{
    public static TAggregate ReplayHistory<TAggregate>(TAggregate aggregate, IEnumerable<IDomainEvent> history)
        where TAggregate : class, IAggregateRoot
    {
        if (aggregate is not IAggregateRootInternal root)
            throw new InvalidOperationException($"Type {typeof(TAggregate).Name} must inherit from AggregateRoot<TEvent>");

        ReplayHistoryCore(root, history);
        return aggregate;
    }

    public static TAggregate FromHistory<TAggregate>(IEnumerable<IDomainEvent> history, Guid id)
        where TAggregate : class, IAggregateRoot
    {
        var aggregate = AggregateFactory<TAggregate>.Create();

        if (aggregate is not IAggregateRootInternal root)
            throw new InvalidOperationException($"Type {typeof(TAggregate).Name} must inherit from AggregateRoot<TEvent>");

        root.SetId(id);
        return ReplayHistory(aggregate, history);
    }

    private static void ReplayHistoryCore<TAggregate>(TAggregate root, IEnumerable<IDomainEvent> history)
        where TAggregate : class, IAggregateRootInternal
    {
        foreach (var e in history)
        {
            root.Apply(e);
            root.IncrementVersion();
        }
    }
}

public static class AggregateFactory<TAggregate>
    where TAggregate : IAggregateRoot
{
    private static readonly Func<TAggregate> _factory;

    static AggregateFactory()
    {
        // Find parameterless ctor
        var ctor = typeof(TAggregate)
            .GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .FirstOrDefault(c => c.GetParameters().Length == 0);

        if (ctor == null)
            throw new InvalidOperationException($"Type {typeof(TAggregate).Name} must have a parameterless constructor");

        var newExp = Expression.New(ctor);
        var lambda = Expression.Lambda<Func<TAggregate>>(newExp);
        _factory = lambda.Compile();
    }

    public static TAggregate Create() => _factory();
}
