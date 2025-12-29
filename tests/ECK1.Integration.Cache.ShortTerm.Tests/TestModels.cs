using ECK1.IntegrationContracts.Abstractions;

namespace ECK1.Integration.Cache.ShortTerm.Tests;

public class Request
{
    public Parent Item { get; set; }

    public ECK1.Integration.EntityStore.CommonDto.Generated.FieldMask Mask { get; set; }
}

public class InnerSubItem
{
    public string InnerItemId { get; set; }
    public string InnerItemString { get; set; }
}

public class InnerItem
{
    public InnerSubItem SubItem { get; set; }
    public string InnerItemId { get; set; }
    public string InnerItemString { get; set; }
}

public class Inner
{
    public string InnerId { get; set; }
    public string InnerString { get; set; }
    public InnerItem SubItem { get; set; }
}

public partial class Parent : IIntegrationMessage
{
    public string ParentId { get; set; }
    public string ParentString { get; set; }
    public Inner Inner { get; set; }
    public List<InnerItem> InnerCollection { get; set; }
    public int Version { get; set; }
    public string Id => ParentId;
}
