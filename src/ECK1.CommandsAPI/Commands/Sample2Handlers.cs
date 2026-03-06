using AutoMapper;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Kafka;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public class Sample2CommandHandlers :
    AggregateCommandHandlerBase<Sample2>,
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
    private readonly IMapper mapper;

    public Sample2CommandHandlers(
        IRootRepository<Sample2> repo,
        IMediator mediator,
        IMapper mapper,
        ILogger<Sample2CommandHandlers> logger)
        : base(repo, mediator, logger)
    {
        this.mapper = mapper;
    }

    public async Task<ICommandResult> Handle(CreateSample2Command command, CancellationToken ct)
    {
        return await SaveAndNotify(
            () => Sample2.Create(command.Customer, command.ShippingAddress, command.LineItems, command.Tags),
            ct);
    }

    public async Task<ICommandResult> Handle(ChangeSample2CustomerEmailCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample2 => sample2.ChangeCustomerEmail(command.NewEmail), ct);
    }

    public async Task<ICommandResult> Handle(ChangeSample2ShippingAddressCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample2 => sample2.ChangeShippingAddress(command.NewAddress), ct);
    }

    public async Task<ICommandResult> Handle(AddSample2LineItemCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample2 => sample2.AddLineItem(command.Item), ct);
    }

    public async Task<ICommandResult> Handle(RemoveSample2LineItemCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample2 => sample2.RemoveLineItem(command.ItemId), ct);
    }

    public async Task<ICommandResult> Handle(ChangeSample2StatusCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample2 => sample2.ChangeStatus(command.NewStatus, command.Reason), ct);
    }

    public async Task<ICommandResult> Handle(AddSample2TagCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample2 => sample2.AddTag(command.Tag), ct);
    }

    public async Task<ICommandResult> Handle(RemoveSample2TagCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample2 => sample2.RemoveTag(command.Tag), ct);
    }

    public async Task<ICommandResult> Handle(RebuildSample2ViewCommand command, CancellationToken ct)
    {
        var sample2 = await Repository.LoadAsync(command.Id, ct);
        if (sample2 is null) return new NotFound();

        await Mediator.Publish(
            new EventNotification<ISample2Event>(mapper.Map<Sample2RebuiltEvent>(sample2), sample2.Version),
            ct);

        return new Success(sample2.Id, []);
    }
}
