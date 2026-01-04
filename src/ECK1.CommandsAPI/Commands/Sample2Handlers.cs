using AutoMapper;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Kafka;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public class Sample2CommandHandlers :
    IRequestHandler<CreateSample2Command, ICommandResult>,
    IRequestHandler<ChangeSample2CustomerEmailCommand, ICommandResult>,
    IRequestHandler<ChangeSample2ShippingAddressCommand, ICommandResult>,
    IRequestHandler<AddSample2LineItemCommand, ICommandResult>,
    IRequestHandler<RemoveSample2LineItemCommand, ICommandResult>,
    IRequestHandler<ChangeSample2StatusCommand, ICommandResult>,
    IRequestHandler<AddSample2TagCommand, ICommandResult>,
    IRequestHandler<RemoveSample2TagCommand, ICommandResult>,
    IRequestHandler<RebuildSample2ViewCommand, ICommandResult>
{
    private readonly Sample2Repo repo;
    private readonly IMediator mediator;
    private readonly IMapper mapper;

    public Sample2CommandHandlers(Sample2Repo repo, IMediator mediator, IMapper mapper)
    {
        this.repo = repo;
        this.mediator = mediator;
        this.mapper = mapper;
    }

    public async Task<ICommandResult> Handle(CreateSample2Command command, CancellationToken ct)
    {
        var sample2 = Sample2.Create(command.Customer, command.ShippingAddress, command.LineItems, command.Tags);
        return await SaveAndNotify(sample2, ct);
    }

    public async Task<ICommandResult> Handle(ChangeSample2CustomerEmailCommand command, CancellationToken ct)
    {
        var sample2 = await repo.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        sample2.ChangeCustomerEmail(command.NewEmail);
        return await SaveAndNotify(sample2, ct);
    }

    public async Task<ICommandResult> Handle(ChangeSample2ShippingAddressCommand command, CancellationToken ct)
    {
        var sample2 = await repo.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        sample2.ChangeShippingAddress(command.NewAddress);
        return await SaveAndNotify(sample2, ct);
    }

    public async Task<ICommandResult> Handle(AddSample2LineItemCommand command, CancellationToken ct)
    {
        var sample2 = await repo.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        sample2.AddLineItem(command.Item);
        return await SaveAndNotify(sample2, ct);
    }

    public async Task<ICommandResult> Handle(RemoveSample2LineItemCommand command, CancellationToken ct)
    {
        var sample2 = await repo.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        sample2.RemoveLineItem(command.ItemId);
        return await SaveAndNotify(sample2, ct);
    }

    public async Task<ICommandResult> Handle(ChangeSample2StatusCommand command, CancellationToken ct)
    {
        var sample2 = await repo.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        sample2.ChangeStatus(command.NewStatus, command.Reason);
        return await SaveAndNotify(sample2, ct);
    }

    public async Task<ICommandResult> Handle(AddSample2TagCommand command, CancellationToken ct)
    {
        var sample2 = await repo.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        sample2.AddTag(command.Tag);
        return await SaveAndNotify(sample2, ct);
    }

    public async Task<ICommandResult> Handle(RemoveSample2TagCommand command, CancellationToken ct)
    {
        var sample2 = await repo.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        sample2.RemoveTag(command.Tag);
        return await SaveAndNotify(sample2, ct);
    }

    public async Task<ICommandResult> Handle(RebuildSample2ViewCommand command, CancellationToken ct)
    {
        var sample2 = await repo.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        await mediator.Publish(
            new EventNotification<ISample2Event>(mapper.Map<Sample2RebuiltEvent>(sample2), sample2.Version),
            ct);

        return new Success(sample2.Id, []);
    }

    private async Task<ICommandResult> SaveAndNotify(Sample2 sample2, CancellationToken ct)
    {
        List<ISample2Event> events = [.. sample2.UncommittedEvents];
        var eventIds = await repo.SaveAsync(sample2, ct);

        foreach (var e in events)
        {
            await mediator.Publish(new EventNotification<ISample2Event>(e, sample2.Version), ct);
        }

        return new Success(sample2.Id, eventIds);
    }
}
