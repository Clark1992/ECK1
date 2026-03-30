using ECK1.CommonUtils.Handler;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;

namespace ECK1.CommandsAPI.Domain;

public interface IAggregateRoot : IAggregateRootReplay
{
    Guid Id { get; }
}

public interface IAggregateRootReplay
{
    int Version { get; }
    void ReplayEvent(IDomainEvent @event);
}

public interface IAggregateRootInternal
{
    IAggregateRootReplay Untouched { get; }
    void InitUntouched();
    IReadOnlyCollection<IDomainEvent> UncommittedEvents { get; }
    void CommitEvents();
}

[HandlerMethod(nameof(Apply))]
public abstract class AggregateRoot<TEvent> : GenericHandler<TEvent>, IAggregateRoot, IAggregateRootInternal
    where TEvent : class, IDomainEvent
{
    protected abstract IAggregateRootReplay DeepClone();

    private readonly List<IDomainEvent> _uncommittedEvents = [];

    public Guid Id { get; protected set; }

    public int Version { get; protected set; }

    [JsonIgnore]
    public IAggregateRootReplay Untouched { get; protected set; }

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

    public void ReplayEvent(IDomainEvent @event)
    {
        Apply((TEvent)@event);
        Version = @event.Version;
    }


    private void Apply(TEvent @event) => Handle(@event);
}

internal static class AggregateRoot
{
    internal static TAggregate FromSnapshot<TAggregate>(TAggregate root, IEnumerable<IDomainEvent> history)
        where TAggregate : class, IAggregateRoot, IAggregateRootInternal
    {
        foreach (var e in history)
        {
            root.ReplayEvent(e);
        }

        root.InitUntouched();

        return root;
    }

    internal static TAggregate CreateNew<TAggregate>()
       where TAggregate : class, IAggregateRoot, IAggregateRootInternal
    {
        var root = AggregateFactory<TAggregate>.Create();
        root.InitUntouched();

        return root;
    }

    public static TAggregate FromStart<TAggregate>(IEnumerable<IDomainEvent> history, Guid id)
        where TAggregate : class, IAggregateRoot, IAggregateRootInternal
    {
        var root = AggregateFactory<TAggregate>.Create();

        return FromSnapshot(root, history);
    }
}

internal static class AggregateFactory<TAggregate>
    where TAggregate : class
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
