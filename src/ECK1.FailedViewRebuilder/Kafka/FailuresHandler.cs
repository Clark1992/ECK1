using AutoMapper;
using ECK1.FailedViewRebuilder.Data;
using ECK1.FailedViewRebuilder.Data.Models;
using ECK1.Kafka;

namespace ECK1.FailedViewRebuilder.Kafka;

public class FailuresHandler(FailuresDbContext db, IMapper mapper) : IKafkaMessageHandler<Contracts.Kafka.BusinessEvents.Sample.SampleEventFailure>
{
    public async Task Handle(string _, Contracts.Kafka.BusinessEvents.Sample.SampleEventFailure message, KafkaMessageId __, CancellationToken ct)
    {
        var existing = await db.SampleEventFailures.FindAsync([message.SampleId], cancellationToken: ct);

        if (existing is null)
        {
            db.SampleEventFailures.Add(mapper.Map<SampleEventFailure>(message));
        }
        else
        {
            mapper.Map(message, existing);
        }

        await db.SaveChangesAsync(ct);
    }
}
