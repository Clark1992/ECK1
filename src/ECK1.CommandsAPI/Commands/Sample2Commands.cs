using ECK1.AsyncApi.Attributes;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Dto.Common;
using ECK1.Contracts.Shared;
using ECK1.Orleans;
using MediatR;
using Orleans;
using System.Text.Json.Serialization;
using Sample2Dto = ECK1.CommandsAPI.Dto.Sample2;

namespace ECK1.CommandsAPI.Commands;

[Newtonsoft.Json.JsonConverter(typeof(Polymorph<ISample2Command>), "$type")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(CreateSample2Command), nameof(CreateSample2Command))]
[JsonDerivedType(typeof(ChangeSample2CustomerEmailCommand), nameof(ChangeSample2CustomerEmailCommand))]
[JsonDerivedType(typeof(ChangeSample2ShippingAddressCommand), nameof(ChangeSample2ShippingAddressCommand))]
[JsonDerivedType(typeof(AddSample2LineItemCommand), nameof(AddSample2LineItemCommand))]
[JsonDerivedType(typeof(RemoveSample2LineItemCommand), nameof(RemoveSample2LineItemCommand))]
[JsonDerivedType(typeof(ChangeSample2StatusCommand), nameof(ChangeSample2StatusCommand))]
[JsonDerivedType(typeof(AddSample2TagCommand), nameof(AddSample2TagCommand))]
[JsonDerivedType(typeof(RemoveSample2TagCommand), nameof(RemoveSample2TagCommand))]
[Command]
[Topic(TopicConfigKey = "Kafka:Sample2CommandsTopic")]
public interface ISample2Command : IGrainKeyResolver<Sample2>, IRequest<(ICommandResult, Sample2)>;

[GenerateSerializer]
[Route("POST", "/api/async/sample2")]
public record CreateSample2Command(Sample2Dto.Customer Customer, Address ShippingAddress, List<Sample2Dto.LineItem> LineItems, List<string> Tags) : ISample2Command;

[GenerateSerializer]
[Route("PUT", "/api/async/sample2/{id}/customer-email")]
public record ChangeSample2CustomerEmailCommand([property: FromRoute("id")] Guid Id, string NewEmail) : ISample2Command, IValueId<Guid>;

[GenerateSerializer]
[Route("PUT", "/api/async/sample2/{id}/shipping-address")]
public record ChangeSample2ShippingAddressCommand([property: FromRoute("id")] Guid Id, Address NewAddress) : ISample2Command, IValueId<Guid>;

[GenerateSerializer]
[Route("POST", "/api/async/sample2/{id}/line-items")]
public record AddSample2LineItemCommand([property: FromRoute("id")] Guid Id, Sample2Dto.LineItem Item) : ISample2Command, IValueId<Guid>;

[GenerateSerializer]
[Route("DELETE", "/api/async/sample2/{id}/line-items/{itemId}")]
public record RemoveSample2LineItemCommand([property: FromRoute("id")] Guid Id, [property: FromRoute("itemId")] Guid ItemId) : ISample2Command, IValueId<Guid>;

[GenerateSerializer]
[Route("PUT", "/api/async/sample2/{id}/status")]
public record ChangeSample2StatusCommand([property: FromRoute("id")] Guid Id, Sample2Status NewStatus, string Reason) : ISample2Command, IValueId<Guid>;

[GenerateSerializer]
[Route("POST", "/api/async/sample2/{id}/tags")]
public record AddSample2TagCommand([property: FromRoute("id")] Guid Id, [property: FromQuery("tag")] string Tag) : ISample2Command, IValueId<Guid>;

[GenerateSerializer]
[Route("DELETE", "/api/async/sample2/{id}/tags")]
public record RemoveSample2TagCommand([property: FromRoute("id")] Guid Id, [property: FromQuery("tag")] string Tag) : ISample2Command, IValueId<Guid>;

[GenerateSerializer]
public class RebuildSample2ViewCommand : RebuildViewCommandBase, IGrainKeyResolver<Sample2>, IRequest<(ICommandResult, Sample2)>;
