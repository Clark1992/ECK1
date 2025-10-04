using ECK1.CommonUtils.Handler;
using ECK1.ReadProjector.Data;
using ECK1.ReadProjector.Events;
using ECK1.ReadProjector.Notifications;
using ECK1.ReadProjector.Views;
using MediatR;
using MongoDB.Driver;
using System.Net.Mail;

namespace ECK1.ReadProjector.Handlers;

[HandlerMethod(nameof(Handle))]
public class SampleEventHandlers(MongoDbContext db, ILogger<SampleEventHandlers> logger) : GenericAsyncHandler<ISampleEvent, SampleView>, //INotificationHandler<EventNotification<ISampleEvent>>
    IRequestHandler<EventWithStateNotification<ISampleEvent, SampleView>, SampleView>
{
    //public Task Handle(EventNotification<ISampleEvent> ev, CancellationToken ct)
    //{
    //    return base.Handle(ev.Event, ct);
    //}

    public Task<SampleView> Handle(EventWithStateNotification<ISampleEvent, SampleView> data, CancellationToken ct)
    {
        return base.Handle(data.Event, data.State, ct);
    }

    public async Task<SampleView> Handle(SampleCreatedEvent @event, SampleView state, CancellationToken cancellationToken)
    {
        var existing = await db.Samples.Find(s => s.SampleId == @event.SampleId).FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            logger.LogWarning("Entity {Id} already exists in {table}", @event.SampleId, nameof(db.Samples));

            return existing;
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

        return view;
    }

    public async Task<SampleView> Handle(SampleNameChangedEvent @event, SampleView state, CancellationToken cancellationToken)
    {
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.Set(s => s.Name, @event.NewName),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.Name = @event.NewName;

        return state;
    }

    public async Task<SampleView> Handle(SampleDescriptionChangedEvent @event, SampleView state, CancellationToken cancellationToken)
    {
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.Set(s => s.Description, @event.NewDescription),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.Description = @event.NewDescription;

        return state;
    }

    public async Task<SampleView> Handle(SampleAddressChangedEvent @event, SampleView state, CancellationToken cancellationToken)
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

        state.Address = address;

        return state;
    }

    public async Task<SampleView> Handle(SampleAttachmentAddedEvent @event, SampleView state, CancellationToken cancellationToken)
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

        state.Attachments.Add(attachment);

        return state;
    }

    public async Task<SampleView> Handle(SampleAttachmentRemovedEvent @event, SampleView state, CancellationToken cancellationToken)
    {
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.PullFilter(s => s.Attachments, a => a.Id == @event.AttachmentId),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.Attachments.RemoveAll(a => a.Id == @event.AttachmentId);

        return state;
    }

    public async Task<SampleView> Handle(SampleAttachmentUpdatedEvent @event, SampleView state, CancellationToken cancellationToken)
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

        var attachment = state.Attachments.FirstOrDefault(a => a.Id == @event.AttachmentId);
        if (attachment is not null)
        {
            attachment.Url = @event.NewUrl;
            attachment.FileName = @event.NewFileName;
        }

        return state;
    }
}
