using AutoMapper;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommandsAPI.Kafka;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public class SampleCommandHandlers :
    AggregateCommandHandlerBase<Sample>,
    IRequestHandler<CreateSampleCommand, ICommandResult>,
    IRequestHandler<ChangeSampleNameCommand, ICommandResult>,
    IRequestHandler<ChangeSampleDescriptionCommand, ICommandResult>,
    IRequestHandler<ChangeSampleAddressCommand, ICommandResult>,
    IRequestHandler<AddSampleAttachmentCommand, ICommandResult>,
    IRequestHandler<RemoveSampleAttachmentCommand, ICommandResult>,
    IRequestHandler<UpdateSampleAttachmentCommand, ICommandResult>,
    IRequestHandler<RebuildSampleViewCommand, ICommandResult>
{
    private readonly IMapper mapper;
    private readonly ILogger<SampleCommandHandlers> logger;

    public SampleCommandHandlers(
        IRootRepository<Sample> repo,
        IMediator mediator,
        IMapper mapper,
        ILogger<SampleCommandHandlers> logger)
        : base(repo, mediator, logger)
    {
        this.mapper = mapper;
        this.logger = logger;
    }

    public async Task<ICommandResult> Handle(CreateSampleCommand command, CancellationToken ct)
    {
        this.logger.LogInformation("Handling {Type} command (Name = '{Name}').", command.GetType(), command.Name);
        return await SaveAndNotify(
            () => Sample.Create(command.Name, command.Description, command.Address),
            ct);
    }

    public async Task<ICommandResult> Handle(ChangeSampleNameCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample => sample.ChangeName(command.NewName), ct);
    }

    public async Task<ICommandResult> Handle(ChangeSampleDescriptionCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample => sample.ChangeDescription(command.NewDescription), ct);
    }

    public async Task<ICommandResult> Handle(ChangeSampleAddressCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample => sample.ChangeAddress(command.NewAddress), ct);
    }

    public async Task<ICommandResult> Handle(AddSampleAttachmentCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample => sample.AddAttachment(command.Attachment), ct);
    }

    public async Task<ICommandResult> Handle(RemoveSampleAttachmentCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Id, sample => sample.RemoveAttachment(command.AttachmentId), ct);
    }

    public async Task<ICommandResult> Handle(UpdateSampleAttachmentCommand command, CancellationToken ct)
    {
        return await SaveAndNotify(
            command.Id,
            sample => sample.UpdateAttachment(command.AttachmentId, command.NewFileName, command.NewUrl),
            ct);
    }

    public async Task<ICommandResult> Handle(RebuildSampleViewCommand command, CancellationToken ct)
    {
        var sample = await Repository.LoadAsync(command.Id, ct);

        if (sample is null) return new NotFound();

        await Mediator.Publish(new EventNotification<ISampleEvent>(mapper.Map<SampleRebuiltEvent>(sample), sample.Version), ct);

        return new Success(sample.Id, []);
    }
}
