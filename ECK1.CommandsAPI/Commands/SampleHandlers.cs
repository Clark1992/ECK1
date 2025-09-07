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
    private readonly SampleRepo _repo;

    public SampleCommandHandlers(SampleRepo repo)
    {
        _repo = repo;
    }

    public async Task<ICommandResult> Handle(CreateSampleCommand command, CancellationToken ct)
    {
        var sample = Sample.Create(command.Name, command.Description, command.Address);
        var eventIds = await _repo.SaveAsync(sample, ct);
        return new Success(sample.Id, eventIds);
    }

    public async Task<ICommandResult> Handle(ChangeSampleNameCommand command, CancellationToken ct)
    {
        var sample = await _repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.ChangeName(command.NewName);
        var eventIds = await _repo.SaveAsync(sample, ct);
        return new Success(sample.Id, eventIds);
    }

    public async Task<ICommandResult> Handle(ChangeSampleDescriptionCommand command, CancellationToken ct)
    {
        var sample = await _repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.ChangeDescription(command.NewDescription);
        var eventIds = await _repo.SaveAsync(sample, ct);
        return new Success(sample.Id, eventIds);
    }

    public async Task<ICommandResult> Handle(ChangeSampleAddressCommand command, CancellationToken ct)
    {
        var sample = await _repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.ChangeAddress(command.NewAddress);
        var eventIds = await _repo.SaveAsync(sample, ct);
        return new Success(sample.Id, eventIds);
    }

    public async Task<ICommandResult> Handle(AddSampleAttachmentCommand command, CancellationToken ct)
    {
        var sample = await _repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.AddAttachment(command.Attachment);
        var eventIds = await _repo.SaveAsync(sample, ct);
        return new Success(sample.Id, eventIds);
    }

    public async Task<ICommandResult> Handle(RemoveSampleAttachmentCommand command, CancellationToken ct)
    {
        var sample = await _repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.RemoveAttachment(command.AttachmentId);
        var eventIds = await _repo.SaveAsync(sample, ct);
        return new Success(sample.Id, eventIds);
    }

    public async Task<ICommandResult> Handle(UpdateSampleAttachmentCommand command, CancellationToken ct)
    {
        var sample = await _repo.LoadAsync(command.Id, ct);
        if (sample == null) return new NotFound();

        sample.UpdateAttachment(command.AttachmentId, command.NewFileName, command.NewUrl);
        var eventIds = await _repo.SaveAsync(sample, ct);
        return new Success(sample.Id, eventIds);
    }
}
