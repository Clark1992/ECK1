using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ECK1.FailedViewRebuilder.Data;
using ECK1.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Linq.Expressions;

namespace ECK1.FailedViewRebuilder.Services;

public class SampleRebuildRequestService(
    IKafkaSimpleProducer<Guid> producer, 
    FailuresDbContext db, 
    IOptionsSnapshot<KafkaSettings> settings) :
    RebuildRequestServiceBase<SampleEventFailure, Guid>(producer, db, settings.Value.SampleEventsRebuildRequestTopic);

public class RebuildRequestServiceBase<TEntity, TMessage>(IKafkaSimpleProducer<TMessage> producer, FailuresDbContext db, string topic)
    where TEntity: class
{
    protected readonly int BatchSize = 1000;

    public async Task SendRebuildRequests<TKey>(
        Expression<Func<TEntity, TKey>> orderBy,
        bool isAsc,
        int? count,
        Func<TEntity, TMessage> valueMapper,
        CancellationToken ct)
    {
        IQueryable<TEntity> failedEventsQuery = db.Set<TEntity>();
        failedEventsQuery = isAsc ? failedEventsQuery.OrderBy(orderBy) : failedEventsQuery.OrderByDescending(orderBy);

        failedEventsQuery = failedEventsQuery.Take(count ?? BatchSize);

        var failedEvents = await failedEventsQuery.ToListAsync();

        await Process(failedEvents, valueMapper, ct);
    }

    private async Task Process(List<TEntity> failedEvents, Func<TEntity, TMessage> msgMapper, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        failedEvents.ForEach(e => producer.ProduceAsync(msgMapper(e), topic, ct));
        db.Set<TEntity>().RemoveRange(failedEvents);
        await db.SaveChangesAsync(ct);
    }
}
