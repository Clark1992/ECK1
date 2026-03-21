using ECK1.AsyncApi.Attributes;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.Orleans;
using MediatR;
using Orleans;
using ECK1.CommandsAPI.Dto.Common;
using ECK1.CommandsAPI.Dto.Sample;

namespace ECK1.CommandsAPI.Commands;

[Command]
[Topic(TopicConfigKey = "Kafka:SampleCommandsTopic")]
public interface ISampleCommand: IGrainKeyResolver<Sample>, IRequest<(ICommandResult, Sample)>;
[GenerateSerializer] public record CreateSampleCommand(string Name, string Description, Address Address): ISampleCommand;
[GenerateSerializer] public record ChangeSampleNameCommand(Guid Id, string NewName): ISampleCommand, IValueId<Guid>;
[GenerateSerializer] public record ChangeSampleDescriptionCommand(Guid Id, string NewDescription): ISampleCommand, IValueId<Guid>;
[GenerateSerializer] public record ChangeSampleAddressCommand(Guid Id, Address NewAddress): ISampleCommand, IValueId<Guid>;
[GenerateSerializer] public record AddSampleAttachmentCommand(Guid Id, Attachment Attachment): ISampleCommand, IValueId<Guid>;
[GenerateSerializer] public record RemoveSampleAttachmentCommand(Guid Id, Guid AttachmentId): ISampleCommand, IValueId<Guid>;
[GenerateSerializer] public record UpdateSampleAttachmentCommand(Guid Id, Guid AttachmentId, string NewFileName, string NewUrl): ISampleCommand, IValueId<Guid>;
[GenerateSerializer] public class RebuildSampleViewCommand: RebuildViewCommandBase, IGrainKeyResolver<Sample>, IRequest<(ICommandResult, Sample)>;
