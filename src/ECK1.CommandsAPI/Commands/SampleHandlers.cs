using AutoMapper;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommandsAPI.Domain.Shared;
using ECK1.CommandsAPI.Kafka;
using ECK1.Kafka;
using ECK1.RealtimeFeedback.Contracts;
using ECK1.VersionTracker.Contracts;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public class SampleCommandHandlers :
    AggregateCommandHandlerBase<Sample>,
    IRequestHandler<CommandRequest<CreateSampleCommand, Sample>, (ICommandResult, Sample)>,
    IRequestHandler<CommandRequest<ChangeSampleNameCommand, Sample>, (ICommandResult, Sample)>,
    IRequestHandler<CommandRequest<ChangeSampleDescriptionCommand, Sample>, (ICommandResult, Sample)>,
    IRequestHandler<CommandRequest<ChangeSampleAddressCommand, Sample>, (ICommandResult, Sample)>,
    IRequestHandler<CommandRequest<AddSampleAttachmentCommand, Sample>, (ICommandResult, Sample)>,
    IRequestHandler<CommandRequest<RemoveSampleAttachmentCommand, Sample>, (ICommandResult, Sample)>,
    IRequestHandler<CommandRequest<UpdateSampleAttachmentCommand, Sample>, (ICommandResult, Sample)>,
    IRequestHandler<CommandRequest<RebuildSampleViewCommand, Sample>, (ICommandResult, Sample)>
{
    private readonly IVersionTrackerService _versionTracker;

    public SampleCommandHandlers(
        IRootRepository<Sample> repo,
        IMediator mediator,
        IKafkaTopicProducer<RealtimeFeedbackEvent> feedbackProducer,
        IVersionTrackerService versionTracker,
        ILogger<SampleCommandHandlers> logger)
        : base(repo, mediator, logger, feedbackProducer)
    {
        _versionTracker = versionTracker;
    }

    public async Task<(ICommandResult, Sample)> Handle(CommandRequest<CreateSampleCommand, Sample> command, CancellationToken ct)
    {
        Logger.LogInformation("Handling {Type} command (Name = '{Name}').", command.Command.GetType(), command.Command.Name);

        var cmd = command.Command;
        var address = cmd.Address is not null
            ? new Address(cmd.Address.Street, cmd.Address.City, cmd.Address.Country)
            : null;
        return await SaveAndNotify(
            () => Sample.Create(command.Command.Name, command.Command.Description, address),
            ct);
    }

    public async Task<(ICommandResult, Sample)> Handle(CommandRequest<ChangeSampleNameCommand, Sample> command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Command.Id, command.State, sample => sample.ChangeName(command.Command.NewName), ct, command.Command.ExpectedVersion);
    }

    public async Task<(ICommandResult, Sample)> Handle(CommandRequest<ChangeSampleDescriptionCommand, Sample> command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Command.Id, command.State, sample => sample.ChangeDescription(command.Command.NewDescription), ct, command.Command.ExpectedVersion);
    }

    public async Task<(ICommandResult, Sample)> Handle(CommandRequest<ChangeSampleAddressCommand, Sample> command, CancellationToken ct)
    {
        var dto = command.Command.NewAddress;
        var newAddress = new Address(dto.Street, dto.City, dto.Country);
        return await SaveAndNotify(command.Command.Id, command.State, sample => sample.ChangeAddress(newAddress), ct, command.Command.ExpectedVersion);
    }

    public async Task<(ICommandResult, Sample)> Handle(CommandRequest<AddSampleAttachmentCommand, Sample> command, CancellationToken ct)
    {
        var dto = command.Command.Attachment;
        var attachment = new SampleAttachment(dto.FileName, dto.Url);
        return await SaveAndNotify(command.Command.Id, command.State, sample => sample.AddAttachment(attachment), ct, command.Command.ExpectedVersion);
    }

    public async Task<(ICommandResult, Sample)> Handle(CommandRequest<RemoveSampleAttachmentCommand, Sample> command, CancellationToken ct)
    {
        return await SaveAndNotify(command.Command.Id, command.State, sample => sample.RemoveAttachment(command.Command.AttachmentId), ct, command.Command.ExpectedVersion);
    }

    public async Task<(ICommandResult, Sample)> Handle(CommandRequest<UpdateSampleAttachmentCommand, Sample> command, CancellationToken ct)
    {
        return await SaveAndNotify(
            command.Command.Id,
            command.State,
            sample => sample.UpdateAttachment(command.Command.AttachmentId, command.Command.NewFileName, command.Command.NewUrl),
            ct,
            command.Command.ExpectedVersion);
    }

    public async Task<(ICommandResult, Sample)> Handle(CommandRequest<RebuildSampleViewCommand, Sample> command, CancellationToken ct)
    {
        Logger.LogInformation("RebuildSampleViewCommand: {Id}:[{Failed}]", command.Command.Id, string.Join(", ", command.Command.FailedTargets));

        return command.Command.IsFullHistoryRebuild ?
            await RebuildFullHistory(command.Command, ct) :
            await RebuildLatest(command.Command, ct);
    }

    public async Task<(ICommandResult, Sample)> RebuildFullHistory(RebuildSampleViewCommand cmd, CancellationToken ct)
    {
        var history = await Repository.LoadHistory(cmd.Id, ct);

        if (history.Count == 0) return (new NotFound(), null);

        var sample = AggregateRoot.CreateNew<Sample>();
        history.ForEach(sample.ReplayEvent);

        await Mediator.Publish(
            new AggregateSavedNotification<Sample>(sample, history, cmd.FailedTargets),
            ct);

        return (new Success(sample.Id, []), sample);
    }

    public async Task<(ICommandResult, Sample)> RebuildLatest(RebuildSampleViewCommand cmd, CancellationToken ct)
    {
        var sample = await Repository.LoadAsync(cmd.Id, ct);
        var latest = await Repository.GetLatestEvent(cmd.Id, ct);

        if (sample is null || latest is null) return (new NotFound(), null);

        await _versionTracker.PutVersion(new PutVersionRequest
        {
            EntityType = $"ECK1.{typeof(Sample).Name}",
            EntityId = sample.Id.ToString(),
            Version = sample.Version
        });

        if (cmd.FailedTargets is not [VersionTrackerConstants.TargetName])
        {
            await Mediator.Publish(
                new AggregateSavedNotification<Sample>(sample, [latest], cmd.FailedTargets),
                ct);
        }

        return (new Success(sample.Id, []), sample);
    }
}
