using AutoMapper;
using ECK1.CommonUtils.Handler;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample2;
using ECK1.ViewProjector.Data;
using ECK1.ViewProjector.Events;
using ECK1.ViewProjector.Notifications;
using ECK1.ViewProjector.Views;
using MediatR;
using MongoDB.Driver;

namespace ECK1.ViewProjector.Handlers;

[HandlerMethod(nameof(Handle))]
public class Sample2EventHandlers(
    IMediator mediator,
    IMapper mapper,
    MongoDbContext db,
    ILogger<Sample2EventHandlers> logger) :
    GenericAsyncHandler<ISample2Event, Sample2View>,
    IRequestHandler<EventMessage<ISample2Event, Sample2View>, Sample2View>
{
    public async Task<Sample2View> Handle(Sample2CreatedEvent @event, Sample2View state, CancellationToken cancellationToken)
    {
        var existing = await db.Sample2s.Find(s => s.Sample2Id == @event.Sample2Id).FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            logger.LogWarning("Entity {Id} already exists in {table}", @event.Sample2Id, nameof(db.Sample2s));
            return existing;
        }

        var view = mapper.Map<Sample2View>(@event);
        await db.Sample2s.InsertOneAsync(view, cancellationToken: cancellationToken);
        return view;
    }

    public async Task<Sample2View> Handle(Sample2CustomerEmailChangedEvent @event, Sample2View state, CancellationToken cancellationToken)
    {
        await db.Sample2s.UpdateOneAsync(
            Builders<Sample2View>.Filter.Eq(s => s.Sample2Id, @event.Sample2Id),
            Builders<Sample2View>.Update.Set(s => s.Customer.Email, @event.NewEmail),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.Customer.Email = @event.NewEmail;
        return state;
    }

    public async Task<Sample2View> Handle(Sample2ShippingAddressChangedEvent @event, Sample2View state, CancellationToken cancellationToken)
    {
        var address = mapper.Map<Sample2AddressView>(@event.NewAddress);

        await db.Sample2s.UpdateOneAsync(
            Builders<Sample2View>.Filter.Eq(s => s.Sample2Id, @event.Sample2Id),
            Builders<Sample2View>.Update.Set(s => s.ShippingAddress, address),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.ShippingAddress = address;
        return state;
    }

    public async Task<Sample2View> Handle(Sample2LineItemAddedEvent @event, Sample2View state, CancellationToken cancellationToken)
    {
        var item = mapper.Map<Sample2LineItemView>(@event.Item);

        await db.Sample2s.UpdateOneAsync(
            Builders<Sample2View>.Filter.Eq(s => s.Sample2Id, @event.Sample2Id),
            Builders<Sample2View>.Update.Push(s => s.LineItems, item),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.LineItems.Add(item);
        return state;
    }

    public async Task<Sample2View> Handle(Sample2LineItemRemovedEvent @event, Sample2View state, CancellationToken cancellationToken)
    {
        await db.Sample2s.UpdateOneAsync(
            Builders<Sample2View>.Filter.Eq(s => s.Sample2Id, @event.Sample2Id),
            Builders<Sample2View>.Update.PullFilter(s => s.LineItems, i => i.ItemId == @event.ItemId),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.LineItems.RemoveAll(i => i.ItemId == @event.ItemId);
        return state;
    }

    public async Task<Sample2View> Handle(Sample2StatusChangedEvent @event, Sample2View state, CancellationToken cancellationToken)
    {
        await db.Sample2s.UpdateOneAsync(
            Builders<Sample2View>.Filter.Eq(s => s.Sample2Id, @event.Sample2Id),
            Builders<Sample2View>.Update.Set(s => s.Status, (int)@event.NewStatus),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.Status = (int)@event.NewStatus;
        return state;
    }

    public async Task<Sample2View> Handle(Sample2TagAddedEvent @event, Sample2View state, CancellationToken cancellationToken)
    {
        await db.Sample2s.UpdateOneAsync(
            Builders<Sample2View>.Filter.Eq(s => s.Sample2Id, @event.Sample2Id),
            Builders<Sample2View>.Update.AddToSet(s => s.Tags, @event.Tag),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        if (!state.Tags.Contains(@event.Tag))
            state.Tags.Add(@event.Tag);
        return state;
    }

    public async Task<Sample2View> Handle(Sample2TagRemovedEvent @event, Sample2View state, CancellationToken cancellationToken)
    {
        await db.Sample2s.UpdateOneAsync(
            Builders<Sample2View>.Filter.Eq(s => s.Sample2Id, @event.Sample2Id),
            Builders<Sample2View>.Update.Pull(s => s.Tags, @event.Tag),
            new UpdateOptions { IsUpsert = false },
            cancellationToken);

        state.Tags.RemoveAll(t => string.Equals(t, @event.Tag, StringComparison.OrdinalIgnoreCase));
        return state;
    }

    public async Task<Sample2View> Handle(Sample2RebuiltEvent @event, Sample2View _, CancellationToken cancellationToken)
    {
        var existing = await db.Sample2s.Find(s => s.Sample2Id == @event.Sample2Id).FirstOrDefaultAsync(cancellationToken);
        var view = mapper.Map<Sample2View>(@event);
        view.Id = existing?.Id ?? default;

        if (existing is null)
        {
            logger.LogWarning("Entity {Id} not found in {table}. Inserting new one.", @event.Sample2Id, nameof(db.Sample2s));
            await db.Sample2s.InsertOneAsync(view, cancellationToken: cancellationToken);
        }
        else
        {
            await db.Sample2s.ReplaceOneAsync(
                Builders<Sample2View>.Filter.Eq(s => s.Sample2Id, @event.Sample2Id),
                view,
                cancellationToken: cancellationToken);
        }

        return view;
    }

    public async Task<Sample2View> Handle(EventMessage<ISample2Event, Sample2View> data, CancellationToken ct)
    {
        try
        {
            var state = data.State switch
            {
                not null => data.State,
                null when data.Event is Sample2CreatedEvent => null,
                _ => await GetState(data.Event.Sample2Id, ct)
            };

            if (state is null && data.Event is not Sample2CreatedEvent)
                throw new InvalidOperationException($"Can't retrieve state for {data.Event.Sample2Id}");

            var newState = await Handle(data.Event, state, ct);

            await mediator.Publish(new EventNotification<Sample2ThinEvent, Sample2View>(new Sample2ThinEvent
            {
                EventType = data.Event.GetType().FullName,
                EventId = data.Event.EventId,
                EntityId = data.Event.Sample2Id,
                OccuredAt = data.Event.OccurredAt.UtcDateTime,
                Version = data.Event.Version
            }, newState), default);

            return newState;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during handling of: {eventType}", data.Event.GetType().FullName);
            await mediator.Publish(new Sample2EventFailure
            {
                FailedEventType = data.Event.GetType().Name,
                EntityId = data.Event.Sample2Id,
                FailureOccurredAt = DateTimeOffset.UtcNow,
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace
            }, default);
            throw;
        }
    }

    private Task<Sample2View> GetState(Guid sample2Id, CancellationToken ct) =>
        db.Sample2s.Find(s => s.Sample2Id == sample2Id).FirstOrDefaultAsync(ct);
}
