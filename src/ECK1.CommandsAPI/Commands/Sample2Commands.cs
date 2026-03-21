using ECK1.AsyncApi.Attributes;
using Sample2Dto = ECK1.CommandsAPI.Dto.Sample2;
using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.Orleans;
using MediatR;
using Orleans;
using ECK1.CommandsAPI.Dto.Common;

namespace ECK1.CommandsAPI.Commands;

[Command]
[Topic(TopicConfigKey = "Kafka:Sample2CommandsTopic")]
public interface ISample2Command : IGrainKeyResolver<Sample2>, IRequest<(ICommandResult, Sample2)>;
[GenerateSerializer] public record CreateSample2Command(Sample2Dto.Customer Customer, Address ShippingAddress, List<Sample2Dto.LineItem> LineItems, List<string> Tags): ISample2Command;
[GenerateSerializer] public record ChangeSample2CustomerEmailCommand(Guid Id, string NewEmail): ISample2Command, IValueId<Guid>;
[GenerateSerializer] public record ChangeSample2ShippingAddressCommand(Guid Id, Address NewAddress): ISample2Command, IValueId<Guid>;
[GenerateSerializer] public record AddSample2LineItemCommand(Guid Id, Sample2Dto.LineItem Item): ISample2Command, IValueId<Guid>;
[GenerateSerializer] public record RemoveSample2LineItemCommand(Guid Id, Guid ItemId): ISample2Command, IValueId<Guid>;
[GenerateSerializer] public record ChangeSample2StatusCommand(Guid Id, Sample2Status NewStatus, string Reason): ISample2Command, IValueId<Guid>;
[GenerateSerializer] public record AddSample2TagCommand(Guid Id, string Tag): ISample2Command, IValueId<Guid>;
[GenerateSerializer] public record RemoveSample2TagCommand(Guid Id, string Tag): ISample2Command, IValueId<Guid>;
[GenerateSerializer] public class RebuildSample2ViewCommand: RebuildViewCommandBase, IGrainKeyResolver<Sample2>, IRequest<(ICommandResult, Sample2)>;

