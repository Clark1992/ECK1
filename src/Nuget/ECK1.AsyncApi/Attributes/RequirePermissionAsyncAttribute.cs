namespace ECK1.AsyncApi.Attributes;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAsyncAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
