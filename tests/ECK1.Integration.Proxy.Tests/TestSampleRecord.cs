using ECK1.IntegrationContracts.Abstractions;

namespace ECK1.Integration.Proxy.Tests;

public class InnerObject
{
    public string InnerName { get; set; }
}

public sealed class TestSampleRecord : IIntegrationMessage
{
    public int SampleId { get; set; }
    public int Version { get; set; }
    public string Name { get; set; }
    public InnerObject Inner { get; set; }

    public Attachment[] Attachments { get; set; }

    public string Id => SampleId.ToString();
}

public sealed class Attachment
{
    public string FileName { get; set; }
    public string Url { get; set; }
    public SubAttachment[] SubAttachments { get; set; }
    public SubSubAttachment[] SubSubAttachments { get; set; }
}

public sealed class SubAttachment
{
    public string SubFileName { get; set; }
    public string SubUrl { get; set; }
    public SubSubAttachment[] SubSubAttachments { get; set; }
}

public sealed class SubSubAttachment
{
    public string SubSubFileName { get; set; }
    public string SubSubUrl { get; set; }
}
