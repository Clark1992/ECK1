using ECK1.CommonUtils.Handler;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ECK1.CommandsAPI.Domain;

public interface IAggregateRoot
{
    Guid Id { get; }
    int Version { get; }
    void ReplayEvent(IDomainEvent @event);
    IReadOnlyCollection<IDomainEvent> UncommittedEvents { get; }
    void CommitEvents();
}

public interface IAggregateRootInternal
{
    IAggregateRoot Untouched { get; }
    void SetId(Guid id);
    void InitUntouched();
}

[HandlerMethod(nameof(Apply))]
public abstract class AggregateRoot<TEvent> : GenericHandler<TEvent>, IAggregateRoot, IAggregateRootInternal
    where TEvent : class, IDomainEvent
{
    private readonly List<IDomainEvent> _uncommittedEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();

    public int Version { get; protected set; } = 0;

    [JsonIgnore]
    public IAggregateRoot Untouched { get; protected set; }

    [JsonIgnore]
    public IReadOnlyCollection<IDomainEvent> UncommittedEvents => _uncommittedEvents.AsReadOnly();

    public void InitUntouched() => Untouched = DeepClone();

    protected void ApplyChange(TEvent @event)
    {
        Apply(@event);
        Version++;
        @event.Version = Version;
        _uncommittedEvents.Add(@event);
    }

    public void CommitEvents()
    {
        _uncommittedEvents.Clear();
        Untouched = DeepClone();
    }

    public void SetId(Guid id) => Id = id;

    public void ReplayEvent(IDomainEvent @event)
    {
        Apply((TEvent)@event);
        Version++;
    }

    void Apply(TEvent @event) => Handle(@event);

    protected abstract IAggregateRoot DeepClone();
}

internal static class AggregateRoot
{
    internal static TAggregate ReplayHistory<TAggregate>(TAggregate aggregate, IEnumerable<IDomainEvent> history)
        where TAggregate : class, IAggregateRoot
    {
        foreach (var e in history)
        {
            aggregate.ReplayEvent(e);
        }

        return aggregate;
    }

    public static TAggregate FromHistory<TAggregate>(IEnumerable<IDomainEvent> history, Guid id)
        where TAggregate : class, IAggregateRoot, IAggregateRootInternal
    {
        var root = AggregateFactory<TAggregate>.Create();

        root.SetId(id);
        return ReplayHistory(root, history);
    }
}

internal static class AggregateFactory<TAggregate>
    where TAggregate : class, IAggregateRootInternal
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
