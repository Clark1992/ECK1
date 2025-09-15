using AutoMapper;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain.Samples;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public class SampleCommandHandlers :
    IRequestHandler<CreateSampleCommand, ICommandResult>,
    IRequestHandler<ChangeSampleNameCommand, ICommandResult>,
    IRequestHandler<ChangeSampleDescriptionCommand, ICommandResult>,
    IRequestHandler<ChangeSampleAddressCommand, ICommandResult>,
    IRequestHandler<AddSampleAttachmentCommand, ICommandResult>,
    IRequestHandler<RemoveSampleAttachmentCommand, ICommandResult>,
    IRequestHandler<UpdateSampleAttachmentCommand, ICommandResult>
{
    private readonly SampleRepo repo;
    private readonly IMediator mediator;

    public SampleCommandHandlers(SampleRepo repo, IMediator mediator)
    {
        this.repo = repo;
        this.mediator = mediator;
    }

    public async Task<ICommandResult> Handle(CreateSampleCommand command, CancellationToken ct)
    {
        var sample = Sample.Create(command.Name, command.Description, command.Address);

        return await SaveAndNotify(sample, ct);
    }

    public async Task<ICommandResult> Handle(ChangeSampleNameCommand command, CancellationToken ct)
    {
        var sample = await repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.ChangeName(command.NewName);

        return await SaveAndNotify(sample, ct);
    }

    public async Task<ICommandResult> Handle(ChangeSampleDescriptionCommand command, CancellationToken ct)
    {
        var sample = await repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.ChangeDescription(command.NewDescription);

        return await SaveAndNotify(sample, ct);
    }

    public async Task<ICommandResult> Handle(ChangeSampleAddressCommand command, CancellationToken ct)
    {
        var sample = await repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.ChangeAddress(command.NewAddress);

        return await SaveAndNotify(sample, ct);
    }

    public async Task<ICommandResult> Handle(AddSampleAttachmentCommand command, CancellationToken ct)
    {
        var sample = await repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.AddAttachment(command.Attachment);

        return await SaveAndNotify(sample, ct);
    }

    public async Task<ICommandResult> Handle(RemoveSampleAttachmentCommand command, CancellationToken ct)
    {
        var sample = await repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.RemoveAttachment(command.AttachmentId);

        return await SaveAndNotify(sample, ct);
    }

    public async Task<ICommandResult> Handle(UpdateSampleAttachmentCommand command, CancellationToken ct)
    {
        var sample = await repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.UpdateAttachment(command.AttachmentId, command.NewFileName, command.NewUrl);

        return await SaveAndNotify(sample, ct);
    }

    private async Task<ICommandResult> SaveAndNotify(Sample sample, CancellationToken ct)
    {
        List<ISampleEvent> events = [.. sample.UncommittedEvents];
        var eventIds = await repo.SaveAsync(sample, ct);

        events.ForEach(e => mediator.Publish(new EventNotification<ISampleEvent>(e), ct));

        return new Success(sample.Id, eventIds);
    }
}
