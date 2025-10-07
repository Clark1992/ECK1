using AutoMapper;
using ECK1.CommandsAPI.Data;
using ECK1.CommandsAPI.Domain.Samples;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace ECK1.CommandsAPI.Commands;

public class SampleCommandHandlers :
    IRequestHandler<CreateSampleCommand, ICommandResult>,
    IRequestHandler<ChangeSampleNameCommand, ICommandResult>,
    IRequestHandler<ChangeSampleDescriptionCommand, ICommandResult>,
    IRequestHandler<ChangeSampleAddressCommand, ICommandResult>,
    IRequestHandler<AddSampleAttachmentCommand, ICommandResult>,
    IRequestHandler<RemoveSampleAttachmentCommand, ICommandResult>,
    IRequestHandler<UpdateSampleAttachmentCommand, ICommandResult>,
    IRequestHandler<RebuildSampleViewCommand, ICommandResult>
{
    private readonly SampleRepo repo;
    private readonly IMediator mediator;
    private readonly IMapper mapper;

    public SampleCommandHandlers(SampleRepo repo, IMediator mediator, IMapper mapper)
    {
        this.repo = repo;
        this.mediator = mediator;
        this.mapper = mapper;
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

    public async Task<ICommandResult> Handle(RebuildSampleViewCommand command, CancellationToken ct)
    {
        var sample = await repo.LoadAsync(command.Id, ct);

        await mediator.Publish(new EventNotification<ISampleEvent>(mapper.Map<SampleRebuiltEvent>(sample)), ct);

        return new Success(sample.Id, new List<Guid> { Guid.Empty });
    }

    private async Task<ICommandResult> SaveAndNotify(Sample sample, CancellationToken ct)
    {
        List<ISampleEvent> events = [.. sample.UncommittedEvents];
        var eventIds = await repo.SaveAsync(sample, ct);

        foreach (var e in events)
        {
            await mediator.Publish(new EventNotification<ISampleEvent>(e), ct);
        }

        return new Success(sample.Id, eventIds);
    }

}
