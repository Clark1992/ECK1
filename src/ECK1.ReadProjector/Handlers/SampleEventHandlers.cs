using Amazon.Runtime.Internal;
using ECK1.CommonUtils.Handler;
using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ECK1.ReadProjector.Data;
using ECK1.ReadProjector.Notifications;
using ECK1.ReadProjector.Views;
using MediatR;
using MongoDB.Driver;

namespace ECK1.ReadProjector.Handlers;

[HandlerMethod(nameof(Handle))]
public class SampleEventHandlers(MongoDbContext db, ILogger<SampleEventHandlers> logger) : GenericAsyncHandler<ISampleEvent>, INotificationHandler<EventNotification<ISampleEvent>>,
{
    public Task Handle(EventNotification<ISampleEvent> ev, CancellationToken ct)
    {
        return base.Handle(ev.Event, ct);
    }

    public async Task Handle(SampleCreatedEvent @event, CancellationToken cancellationToken)
    {
        var existing = await db.Samples.Find(s => s.SampleId == @event.SampleId).FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            logger.LogWarning("Entity {Id} already exists in {table}", @event.SampleId, nameof(db.Samples));

            return;
        }

        var view = new SampleView
        {
            SampleId = @event.SampleId,
            Name = @event.Name,
            Description = @event.Description,
            Address = new SampleAddressView
            {
                Id = Guid.NewGuid(),
                Street = @event.Address.Street,
                City = @event.Address.City,
                Country = @event.Address.Country
            }
        };

        await db.Samples.InsertOneAsync(view, cancellationToken: cancellationToken);
    }

    public async Task Handle(SampleNameChangedEvent @event, CancellationToken cancellationToken)
    {
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.Set(s => s.Name, @event.NewName),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task Handle(SampleDescriptionChangedEvent @event, CancellationToken cancellationToken)
    {
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.Set(s => s.Description, @event.NewDescription),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task Handle(SampleAddressChangedEvent @event, CancellationToken cancellationToken)
    {
        var address = new SampleAddressView
        {
            Id = Guid.NewGuid(),
            Street = @event.NewAddress.Street,
            City = @event.NewAddress.City,
            Country = @event.NewAddress.Country
        };

        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.Set(s => s.Address, address),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task Handle(SampleAttachmentAddedEvent @event, CancellationToken cancellationToken)
    {
        var attachment = new SampleAttachmentView
        {
            Id = @event.Attachment.Id,
            FileName = @event.Attachment.FileName,
            Url = @event.Attachment.Url
        };

        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.Push(s => s.Attachments, attachment),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task Handle(SampleAttachmentRemovedEvent @event, CancellationToken cancellationToken)
    {
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.PullFilter(s => s.Attachments, a => a.Id == @event.AttachmentId),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task Handle(SampleAttachmentUpdatedEvent @event, CancellationToken cancellationToken)
    {
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.And(
                Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
                Builders<SampleView>.Filter.ElemMatch(s => s.Attachments, a => a.Id == @event.AttachmentId)
            ),
            Builders<SampleView>.Update
                .Set("Attachments.$.FileName", @event.NewFileName)
                .Set("Attachments.$.Url", @event.NewUrl),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }
}
