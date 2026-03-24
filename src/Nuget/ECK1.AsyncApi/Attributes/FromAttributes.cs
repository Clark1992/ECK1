namespace ECK1.AsyncApi.Attributes;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class FromRouteAttribute(string Name = null) : Attribute
{
    public string Name { get; set; } = Name;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class FromBodyAttribute(string Name = null) : Attribute
{
    public string Name { get; set; } = Name;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class FromQueryAttribute(string Name = null) : Attribute
{
    public string Name { get; set; } = Name;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class FromClaimAttribute(string Name) : Attribute
{
    public string Name { get; set; } = Name;
}

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class FromHeaderAttribute(string Name = null) : Attribute
{
    public string Name { get; set; } = Name;
}