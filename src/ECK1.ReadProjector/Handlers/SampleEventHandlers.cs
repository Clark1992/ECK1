using MediatR;
using MongoDB.Driver;
using ECK1.ReadProjector.Data;
using ECK1.ReadProjector.Views;
using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using ECK1.ReadProjector.Notifications;

namespace ECK1.ReadProjector.Handlers;

public class SampleEventHandlers(MongoDbContext db) : 
    IRequestHandler<EventNotification<ISampleEvent>>
    //IRequestHandler<EventNotification<SampleCreatedEvent>>,
    //IRequestHandler<EventNotification<SampleNameChangedEvent>>,
    //IRequestHandler<EventNotification<SampleDescriptionChangedEvent>>,
    //IRequestHandler<EventNotification<SampleAddressChangedEvent>>,
    //IRequestHandler<EventNotification<SampleAttachmentAddedEvent>>,
    //IRequestHandler<EventNotification<SampleAttachmentRemovedEvent>>,
    //IRequestHandler<EventNotification<SampleAttachmentUpdatedEvent>>

{
    public Task Handle(EventNotification<ISampleEvent> request, CancellationToken cancellationToken)
    {
        throw new NotImplementedException();
    }

    public async Task Handle(EventNotification<SampleCreatedEvent> notification, CancellationToken cancellationToken)
    {
        var @event = notification.Event;
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

    public async Task Handle(EventNotification<SampleNameChangedEvent> notification, CancellationToken cancellationToken)
    {
        var @event = notification.Event;
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.Set(s => s.Name, @event.NewName),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task Handle(EventNotification<SampleDescriptionChangedEvent> notification, CancellationToken cancellationToken)
    {
        var @event = notification.Event;
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.Set(s => s.Description, @event.NewDescription),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task Handle(EventNotification<SampleAddressChangedEvent> notification, CancellationToken cancellationToken)
    {
        var @event = notification.Event;
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

    public async Task Handle(EventNotification<SampleAttachmentAddedEvent> notification, CancellationToken cancellationToken)
    {
        var @event = notification.Event;
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

    public async Task Handle(EventNotification<SampleAttachmentRemovedEvent> notification, CancellationToken cancellationToken)
    {
        var @event = notification.Event;
        await db.Samples.UpdateOneAsync(
            Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
            Builders<SampleView>.Update.PullFilter(s => s.Attachments, a => a.Id == @event.AttachmentId),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);
    }

    public async Task Handle(EventNotification<SampleAttachmentUpdatedEvent> notification, CancellationToken cancellationToken)
    {
        var @event = notification.Event;
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
