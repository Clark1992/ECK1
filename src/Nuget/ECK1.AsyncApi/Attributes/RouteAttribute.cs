namespace ECK1.AsyncApi.Attributes;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class)]
public class RouteAttribute(string Method, string Route) : Attribute
{
    public string Method { get; set; } = Method ?? "POST";
    public string Route { get; set; } = Route;
}
