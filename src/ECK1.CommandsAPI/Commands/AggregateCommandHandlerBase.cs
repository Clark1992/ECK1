using System.Diagnostics;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain;
using ECK1.CommandsAPI.Kafka;
using ECK1.Kafka;
using ECK1.RealtimeFeedback.Contracts;
using MediatR;

namespace ECK1.CommandsAPI.Commands;
public abstract class AggregateCommandHandlerBase<TAggregate>
    where TAggregate : class, IAggregateRoot, IAggregateRootInternal
{
    private const int MaxSaveRetries = 2;

    protected AggregateCommandHandlerBase(
        IRootRepository<TAggregate> repository,
        IMediator mediator,
        ILogger logger,
        IKafkaTopicProducer<RealtimeFeedbackEvent> feedbackProducer)
    {
        Repository = repository;
        Mediator = mediator;
        Logger = logger;
        FeedbackProducer = feedbackProducer;
    }

    protected IRootRepository<TAggregate> Repository { get; }
    protected IMediator Mediator { get; }
    protected ILogger Logger { get; }
    protected IKafkaTopicProducer<RealtimeFeedbackEvent> FeedbackProducer { get; }

    protected async Task<(ICommandResult, TAggregate)> SaveAndNotify(Func<TAggregate> aggregateFactory, CancellationToken ct)
    {
        TAggregate aggregate;
        try
        {
            aggregate = aggregateFactory();
        }
        catch (Exception ex)
        {
            var error = new Error { ErrorMessage = ex.Message };
            await SendFeedbackAsync(null, error);
            return (error, null);
        }

        var result = await TrySaveAndNotify(aggregate, ct);

        if (result is VersionConflict conflict)
            await SendFeedbackAsync(aggregate, conflict);

        return (result, aggregate);
    }

    protected async Task<(ICommandResult, TAggregate)> SaveAndNotify(Guid aggregateId, TAggregate state, Action<TAggregate> applyCommand, CancellationToken ct, int? expectedVersion = null)
    {
        var aggregate = state ?? await Repository.LoadAsync(aggregateId, ct);
        if (aggregate is null)
        {
            var notFound = new NotFound();
            await SendFeedbackAsync(null, notFound);
            return (notFound, null);
        }

        if (expectedVersion is not null && aggregate.Version != expectedVersion.Value)
        {
            var versionConflict = new VersionConflict(
                aggregate.Version,
                expectedVersion.Value,
                typeof(TAggregate).Name,
                aggregate.Id);

            await SendFeedbackAsync(aggregate, versionConflict);
            return (versionConflict, null);
        }

        try
        {
            applyCommand(aggregate);
        }
        catch (Exception ex)
        {
            var error = new Error { ErrorMessage = ex.Message };
            await SendFeedbackAsync(aggregate, error);
            return (error, aggregate);
        }

        var result = await TrySaveAndNotify(aggregate, ct);

        if (result is VersionConflict conflict)
            await SendFeedbackAsync(aggregate, conflict);

        return (result, aggregate);
    }

    private async Task<ICommandResult> TrySaveAndNotify(TAggregate aggregate, CancellationToken ct)
    {
        List<IDomainEvent> events = [.. aggregate.UncommittedEvents];

        try
        {
            var eventIds = await Repository.SaveAsync(aggregate, ct);

            await Mediator.Publish(new AggregateSavedNotification<TAggregate>(aggregate, events), ct);

            aggregate.CommitEvents();
            var result = new Success(aggregate.Id, eventIds);
            await SendFeedbackAsync(aggregate, result);
            return result;
        }
        catch (ConcurrencyConflictException ex)
        {
            var aggregateType = typeof(TAggregate).Name;
            Logger.LogWarning(ex,
                "Optimistic concurrency conflict for {Aggregate} [{AggregateId}] while saving command.",
                aggregateType,
                ex.AggregateId);

            return new VersionConflict(
                aggregate?.Version ?? 0,
                aggregateType,
                ex.AggregateId,
                ex.Message);
        }
    }

    private async Task SendFeedbackAsync(TAggregate aggregate, ICommandResult result)
    {
        var activity = Activity.Current;
        var userId = activity?.GetBaggageItem("actor_id") ?? "system";
        var correlationId = activity?.GetBaggageItem("realtime_correlation_id") ?? string.Empty;

        var (success, outcomeCode, message) = result.GetOutcomeData();
        var entityType = typeof(TAggregate).Name;

        string title = success
            ? "Command accepted"
            : outcomeCode switch
            {
                "VERSION_CONFLICT" => "Version conflict",
                "NOT_FOUND" => "Not found",
                _ => "Error"
            };

        if (success && string.IsNullOrEmpty(message))
            message = $"{entityType} is being processed";

        var feedbackEvent = new RealtimeFeedbackEvent
        {
            CorrelationId = correlationId,
            UserId = userId,
            EntityType = entityType,
            EntityId = aggregate?.Id.ToString() ?? string.Empty,
            Success = success,
            OutcomeCode = outcomeCode,
            Title = title,
            Message = message,
            Version = aggregate?.Version ?? 0,
            Timestamp = DateTimeOffset.UtcNow
        };

        try
        {
            await FeedbackProducer.ProduceAsync(feedbackEvent, userId, CancellationToken.None);
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Failed to send realtime feedback for {EntityType}:{EntityId}",
                entityType, aggregate?.Id);
        }
    }
}
