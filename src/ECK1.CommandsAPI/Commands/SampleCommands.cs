using System.Text.Json.Serialization;
using ECK1.AsyncApi.Attributes;
using ECK1.CommandsAPI.Domain.Samples;
using ECK1.CommandsAPI.Dto.Common;
using ECK1.CommandsAPI.Dto.Sample;
using ECK1.CommonUtils.Swagger;
using ECK1.Contracts.Shared;
using ECK1.Orleans;
using MediatR;
using Orleans;

namespace ECK1.CommandsAPI.Commands;

[Newtonsoft.Json.JsonConverter(typeof(Polymorph<ISampleCommand>), "$type")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CreateSampleCommand), nameof(CreateSampleCommand))]
[JsonDerivedType(typeof(ChangeSampleNameCommand), nameof(ChangeSampleNameCommand))]
[JsonDerivedType(typeof(ChangeSampleDescriptionCommand), nameof(ChangeSampleDescriptionCommand))]
[JsonDerivedType(typeof(ChangeSampleAddressCommand), nameof(ChangeSampleAddressCommand))]
[JsonDerivedType(typeof(AddSampleAttachmentCommand), nameof(AddSampleAttachmentCommand))]
[JsonDerivedType(typeof(RemoveSampleAttachmentCommand), nameof(RemoveSampleAttachmentCommand))]
[JsonDerivedType(typeof(UpdateSampleAttachmentCommand), nameof(UpdateSampleAttachmentCommand))]
[Command]
[Topic(TopicConfigKey = "Kafka:SampleCommandsTopic")]
public interface ISampleCommand : IGrainKeyResolver<Sample>, IRequest<(ICommandResult, Sample)>;

[GenerateSerializer]
[Route("POST", "/api/async/sample")]
public record CreateSampleCommand(string Name, string Description, Address Address) : ISampleCommand;

[GenerateSerializer]
[Route("PUT", "/api/async/sample/{id}/name")]
public record ChangeSampleNameCommand([property: FromRoute("id")] Guid Id, string NewName, int ExpectedVersion) : ISampleCommand, IValueId<Guid>;

[GenerateSerializer]
[Route("PUT", "/api/async/sample/{id}/description")]
public record ChangeSampleDescriptionCommand([property: FromRoute("id")] Guid Id, string NewDescription, int ExpectedVersion) : ISampleCommand, IValueId<Guid>;

[GenerateSerializer]
[Route("PUT", "/api/async/sample/{id}/address")]
public record ChangeSampleAddressCommand([property: FromRoute("id")] Guid Id, Address NewAddress, int ExpectedVersion) : ISampleCommand, IValueId<Guid>;

[GenerateSerializer]
[Route("POST", "/api/async/sample/{id}/attachments")]
public record AddSampleAttachmentCommand([property: FromRoute("id")] Guid Id, Attachment Attachment, int ExpectedVersion) : ISampleCommand, IValueId<Guid>;

[GenerateSerializer]
[Route("DELETE", "/api/async/sample/{id}/attachments/{attachmentId}")]
[RequirePermissionAsync("delete")]
public record RemoveSampleAttachmentCommand([property: FromRoute("id")] Guid Id, [property: FromRoute("attachmentId")] Guid AttachmentId, int ExpectedVersion) : ISampleCommand, IValueId<Guid>;

[GenerateSerializer]
[Route("PUT", "/api/async/sample/{id}/attachments/{attachmentId}")]
public record UpdateSampleAttachmentCommand([property: FromRoute("id")] Guid Id, [property: FromRoute("attachmentId")] Guid AttachmentId, string NewFileName, string NewUrl, int ExpectedVersion) : ISampleCommand, IValueId<Guid>;

[GenerateSerializer]
public class RebuildSampleViewCommand : RebuildViewCommandBase, IGrainKeyResolver<Sample>, IRequest<(ICommandResult, Sample)>;
