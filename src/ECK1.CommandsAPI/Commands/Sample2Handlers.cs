using AutoMapper;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Domain.Shared;
using ECK1.CommandsAPI.Kafka;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public class Sample2CommandHandlers :
    AggregateCommandHandlerBase<Sample2>,
    IRequestHandler<CommandRequest<CreateSample2Command, Sample2>, (ICommandResult, Sample2)>,
    IRequestHandler<CommandRequest<ChangeSample2CustomerEmailCommand, Sample2>, (ICommandResult, Sample2)>,
    IRequestHandler<CommandRequest<ChangeSample2ShippingAddressCommand, Sample2>, (ICommandResult, Sample2)>,
    IRequestHandler<CommandRequest<AddSample2LineItemCommand, Sample2>, (ICommandResult, Sample2)>,
    IRequestHandler<CommandRequest<RemoveSample2LineItemCommand, Sample2>, (ICommandResult, Sample2)>,
    IRequestHandler<CommandRequest<ChangeSample2StatusCommand, Sample2>, (ICommandResult, Sample2)>,
    IRequestHandler<CommandRequest<AddSample2TagCommand, Sample2>, (ICommandResult, Sample2)>,
    IRequestHandler<CommandRequest<RemoveSample2TagCommand, Sample2>, (ICommandResult, Sample2)>,
    IRequestHandler<CommandRequest<RebuildSample2ViewCommand, Sample2>, (ICommandResult, Sample2)>
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

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<CreateSample2Command, Sample2> command, CancellationToken ct)
    {
        var cmd = command.Command;
        var customer = new Sample2Customer(cmd.Customer.Email, cmd.Customer.Segment);
        var address = new Address(cmd.ShippingAddress.Street, cmd.ShippingAddress.City, cmd.ShippingAddress.Country);
        var lineItems = cmd.LineItems.Select(li => 
            new Sample2LineItem(
                li.Sku,
                li.Quantity,
                new Sample2Money(li.UnitPrice.Amount, li.UnitPrice.Currency))).ToList();
        return await SaveAndNotify(
            () => Sample2.Create(customer, address, lineItems, command.Command.Tags),
            ct);
    }

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<ChangeSample2CustomerEmailCommand, Sample2> command, CancellationToken ct)
    {
        return await SaveAndNotify(
            command.Command.Id,
            command.State,
            sample2 => sample2.ChangeCustomerEmail(command.Command.NewEmail),
            ct);
    }

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<ChangeSample2ShippingAddressCommand, Sample2> command, CancellationToken ct)
    {
        var dto = command.Command.NewAddress;
        var newAddress = new Address(dto.Street, dto.City, dto.Country);
        return await SaveAndNotify(command.Command.Id, command.State, sample2 => sample2.ChangeShippingAddress(newAddress), ct);
    }

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<AddSample2LineItemCommand, Sample2> command, CancellationToken ct)
    {
        var dto = command.Command.Item;
        var lineItem = new Sample2LineItem(dto.Sku, dto.Quantity, new Sample2Money(dto.UnitPrice.Amount, dto.UnitPrice.Currency));
        return await SaveAndNotify(command.Command.Id, command.State, sample2 => sample2.AddLineItem(lineItem), ct);
    }

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<RemoveSample2LineItemCommand, Sample2> command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Command.Id, command.State, sample2 => sample2.RemoveLineItem(command.Command.ItemId), ct);
    }

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<ChangeSample2StatusCommand, Sample2> command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Command.Id, command.State, sample2 => sample2.ChangeStatus(command.Command.NewStatus, command.Command.Reason), ct);
    }

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<AddSample2TagCommand, Sample2> command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Command.Id, command.State, sample2 => sample2.AddTag(command.Command.Tag), ct);
    }

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<RemoveSample2TagCommand, Sample2> command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Command.Id, command.State, sample2 => sample2.RemoveTag(command.Command.Tag), ct);
    }

    public async Task<(ICommandResult, Sample2)> Handle(CommandRequest<RebuildSample2ViewCommand, Sample2> command, CancellationToken ct)
    {
        var sample2 = await Repository.LoadAsync(command.Command.Id, ct);

        if (sample2 is null) return (new NotFound(), null);

        await Mediator.Publish(
            new AggregateSavedNotification<Sample2>(sample2, [mapper.Map<Sample2RebuiltEvent>(sample2)]),
            ct);

        return (new Success(sample2.Id, []), sample2);
    }
}
