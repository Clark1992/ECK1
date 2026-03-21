using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain;
using ECK1.CommandsAPI.Kafka;
using MediatR;

namespace ECK1.CommandsAPI.Commands;
public abstract class AggregateCommandHandlerBase<TAggregate>
    where TAggregate : class, IAggregateRootInternal, IAggregateRoot
{
    private const int MaxSaveRetries = 2;

    protected AggregateCommandHandlerBase(
        IRootRepository<TAggregate> repository,
        IMediator mediator,
        ILogger logger)
    {
        Repository = repository;
        Mediator = mediator;
        Logger = logger;
    }

    protected IRootRepository<TAggregate> Repository { get; }
    protected IMediator Mediator { get; }
    protected ILogger Logger { get; }

    protected async Task<(ICommandResult, TAggregate)> SaveAndNotify(Func<TAggregate> aggregateFactory, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxSaveRetries; attempt++)
        {
            var aggregate = aggregateFactory();
            var result = await TrySaveAndNotify(aggregate, ct);

            if (result is ConcurrencyConflict && attempt < MaxSaveRetries)
            {
                await Task.Delay(GetRetryDelay(attempt), ct);
                continue;
            }

            return (result, aggregate);
        }

        throw new InvalidOperationException("Unexpected retry loop termination.");
    }

    protected async Task<(ICommandResult, TAggregate)> SaveAndNotify(Guid aggregateId, TAggregate state, Action<TAggregate> applyCommand, CancellationToken ct)
    {
        for (var attempt = 0; attempt <= MaxSaveRetries; attempt++)
        {
            var aggregate = state ?? await Repository.LoadAsync(aggregateId, ct);
            if (aggregate is null)
            {
                return (new NotFound(), null);
            }

            applyCommand(aggregate);

            var result = await TrySaveAndNotify(aggregate, ct);
            if (result is ConcurrencyConflict && attempt < MaxSaveRetries)
            {
                await Task.Delay(GetRetryDelay(attempt), ct);
                continue;
            }

            return (result, aggregate);
        }

        throw new InvalidOperationException("Unexpected retry loop termination.");
    }

    private async Task<ICommandResult> TrySaveAndNotify(TAggregate aggregate, CancellationToken ct)
    {
        List<IDomainEvent> events = [.. aggregate.UncommittedEvents];

        try
        {
            var eventIds = await Repository.SaveAsync(aggregate, ct);

            await Mediator.Publish(new AggregateSavedNotification<TAggregate>(aggregate.Untouched, events), ct);

            aggregate.CommitEvents();
            return new Success(aggregate.Id, eventIds);
        }
        catch (ConcurrencyConflictException ex)
        {
            var aggregateType = aggregate.GetType().Name;
            Logger.LogWarning(ex,
                "Optimistic concurrency conflict for {Aggregate} [{AggregateId}] while saving command.",
                aggregateType,
                ex.AggregateId);

            return new ConcurrencyConflict(
                aggregateType,
                ex.AggregateId,
                ex.Message,
                retryable: true);
        }
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        var baseDelayMs = 15 * (1 << Math.Min(attempt, 4));
        var jitterMs = Random.Shared.Next(0, 15);
        return TimeSpan.FromMilliseconds(baseDelayMs + jitterMs);
    }
}
