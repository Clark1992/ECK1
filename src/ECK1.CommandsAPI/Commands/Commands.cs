using ECK1.CommandsAPI.Domain.Sample2s;
using ECK1.CommandsAPI.Domain.Samples;
using MediatR;

namespace ECK1.CommandsAPI.Commands;

public interface ICommandResult { }

public class Success : ICommandResult 
{ 
    public Success() { }
    public Success(Guid id, List<Guid> eventIds)
    {
        Id = id;
        EventIds = eventIds;
    }

    public Guid Id { get; set; }
    public List<Guid> EventIds { get; set; }
}
public class NotFound : ICommandResult { }

public class Error : ICommandResult { public string ErrorMessage { get; set; } }


public record CreateSampleCommand(string Name, string Description, SampleAddress Address) : IRequest<ICommandResult>;
public record ChangeSampleNameCommand(Guid Id, string NewName) : IRequest<ICommandResult>;
public record ChangeSampleDescriptionCommand(Guid Id, string NewDescription) : IRequest<ICommandResult>;
public record ChangeSampleAddressCommand(Guid Id, SampleAddress NewAddress) : IRequest<ICommandResult>;
public record AddSampleAttachmentCommand(Guid Id, SampleAttachment Attachment) : IRequest<ICommandResult>;
public record RemoveSampleAttachmentCommand(Guid Id, Guid AttachmentId) : IRequest<ICommandResult>;
public record UpdateSampleAttachmentCommand(Guid Id, Guid AttachmentId, string NewFileName, string NewUrl) : IRequest<ICommandResult>;
public record RebuildSampleViewCommand(Guid Id) : IRequest<ICommandResult>;

public record CreateSample2Command(Sample2Customer Customer, Sample2Address ShippingAddress, List<Sample2LineItem> LineItems, List<string> Tags) : IRequest<ICommandResult>;
public record ChangeSample2CustomerEmailCommand(Guid Id, string NewEmail) : IRequest<ICommandResult>;
public record ChangeSample2ShippingAddressCommand(Guid Id, Sample2Address NewAddress) : IRequest<ICommandResult>;
public record AddSample2LineItemCommand(Guid Id, Sample2LineItem Item) : IRequest<ICommandResult>;
public record RemoveSample2LineItemCommand(Guid Id, Guid ItemId) : IRequest<ICommandResult>;
public record ChangeSample2StatusCommand(Guid Id, Sample2Status NewStatus, string Reason) : IRequest<ICommandResult>;
public record AddSample2TagCommand(Guid Id, string Tag) : IRequest<ICommandResult>;
public record RemoveSample2TagCommand(Guid Id, string Tag) : IRequest<ICommandResult>;
public record RebuildSample2ViewCommand(Guid Id) : IRequest<ICommandResult>;

