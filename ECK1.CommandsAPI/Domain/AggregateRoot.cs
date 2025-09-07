using System.Linq.Expressions;
using System.Reflection;

namespace ECK1.CommandsAPI.Domain;

public abstract class AggregateRootHandler<TEvent>
{
    private static readonly Dictionary<Type, Action<AggregateRootHandler<TEvent>, TEvent>> _handlers
        = new();

    static AggregateRootHandler()
    {
        var aggregateType = typeof(AggregateRootHandler<TEvent>).Assembly
            .GetTypes()
            .Where(t => typeof(AggregateRootHandler<TEvent>).IsAssignableFrom(t));

        foreach (var type in aggregateType)
        {
            var methods = type.GetMethods(
                BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Public);

            foreach (var method in methods.Where(m => m.Name == "Apply"))
            {
                var parameters = method.GetParameters();
                if (parameters.Length == 2 &&
                    parameters[0].ParameterType.IsAssignableTo(type) &&
                    typeof(TEvent).IsAssignableFrom(parameters[1].ParameterType))
                {
                    var eventType = parameters[1].ParameterType;

                    // Build delegate: (aggregate, event) => method(aggregate, event)
                    var aggParam = Expression.Parameter(typeof(AggregateRootHandler<TEvent>), "agg");
                    var evtParam = Expression.Parameter(typeof(TEvent), "evt");

                    var call = Expression.Call(
                        method,
                        Expression.Convert(aggParam, type),
                        Expression.Convert(evtParam, eventType));

                    var lambda = Expression.Lambda<Action<AggregateRootHandler<TEvent>, TEvent>>(
                        call, aggParam, evtParam).Compile();

                    _handlers[eventType] = lambda;
                }
            }
        }
    }

    protected void Apply(TEvent @event)
    {
        if (_handlers.TryGetValue(@event.GetType(), out var handler))
        {
            handler(this, @event);
        }
        else
        {
            throw new InvalidOperationException(
                $"No Apply method found for {@event.GetType().Name}");
        }
    }
}


public abstract class AggregateRoot<TEvent> : AggregateRootHandler<TEvent>
{
    private readonly List<TEvent> _uncommittedEvents = new();

    public Guid Id { get; protected set; } = Guid.NewGuid();
    public int Version { get; protected set; } = 0;

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
