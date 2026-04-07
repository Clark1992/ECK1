namespace ECK1.CommonUtils.Swagger;

[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute(string permission) : Attribute
{
    public string Permission { get; } = permission;
}
