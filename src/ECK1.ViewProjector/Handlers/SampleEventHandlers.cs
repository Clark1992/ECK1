using AutoMapper;
using ECK1.CommonUtils.Handler;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using ECK1.Kafka;
using ECK1.ViewProjector.Data;
using ECK1.ViewProjector.Events;
using ECK1.ViewProjector.Handlers.Services;
using ECK1.ViewProjector.Notifications;
using ECK1.ViewProjector.Views;
using MediatR;
using MongoDB.Driver;

namespace ECK1.ViewProjector.Handlers;

[HandlerMethod(nameof(Handle))]
public class SampleEventHandlers(
    IMediator mediator,
    IMapper mapper,
    MongoDbContext db,
    ILogger<SampleEventHandlers> logger) : 
    GenericAsyncHandler<ISampleEvent, SampleView>,
    IRequestHandler<EventMessage<ISampleEvent, SampleView>, SampleView>
{
    public async Task<SampleView> Handle(SampleCreatedEvent @event, SampleView state, CancellationToken cancellationToken)
    {
        var existing = await db.Samples.Find(s => s.SampleId == @event.SampleId).FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            logger.LogWarning("Entity {Id} already exists in {table}", @event.SampleId, nameof(db.Samples));

            return existing;
        }

        var view = mapper.Map<SampleView>(@event);

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
        throw new Exception("Test exception");
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
        var address = mapper.Map<SampleAddressView>(@event.NewAddress);

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
        var attachment = mapper.Map<SampleAttachmentView>(@event.Attachment);

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

    public async Task<SampleView> Handle(SampleRebuiltEvent @event, SampleView _, CancellationToken cancellationToken)
    {
        var existing = await db.Samples.Find(s => s.SampleId == @event.SampleId).FirstOrDefaultAsync(cancellationToken);
        var view = mapper.Map<SampleView>(@event);
        view.Id = existing.Id;

        if (existing is null)
        {
            logger.LogWarning("Entity {Id} not found in {table}. Inserting new one.", @event.SampleId, nameof(db.Samples));

            await db.Samples.InsertOneAsync(
                view,
                cancellationToken: cancellationToken);
        }
        else
        {
            await db.Samples.ReplaceOneAsync(
                Builders<SampleView>.Filter.Eq(s => s.SampleId, @event.SampleId),
                view,
                cancellationToken: cancellationToken);
        }

        return view;
    }

    public async Task<SampleView> Handle(EventMessage<ISampleEvent, SampleView> data, CancellationToken ct)
    {
        try
        {
            var state = data.State switch
            {
                not null => data.State,
                null when data.Event is SampleCreatedEvent => null,
                _ => await GetState(data.Event.SampleId, ct)
            };

            if (state is null && data.Event is not SampleCreatedEvent)
            {
                throw new InvalidOperationException($"Can't retrieve state for {data.Event.SampleId}");
            }

            var newState = await Handle(data.Event, state, ct);

            await mediator.Publish(new EventNotification<SampleThinEvent, SampleView>(new SampleThinEvent
            {
                EventType = data.Event.GetType().FullName,
                EventId = data.Event.EventId,
                EntityId = data.Event.SampleId,
                OccuredAt = data.Event.OccurredAt.UtcDateTime,
                Version = data.Event.Version
            }, newState), default);

            return newState;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during handling of: {eventType}", data.Event.GetType().FullName);
            await mediator.Publish(new EventFailure
            {
                FailedEventType = data.Event.GetType().Name,
                EntityType = "Sample",
                EntityId = data.Event.SampleId,
                FailureOccurredAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace
            }, default);
            throw;
        }
    }

    private Task<SampleView> GetState(Guid sampleId, CancellationToken ct) => 
        db.Samples.Find(s => s.SampleId == sampleId).FirstOrDefaultAsync(ct);
}
