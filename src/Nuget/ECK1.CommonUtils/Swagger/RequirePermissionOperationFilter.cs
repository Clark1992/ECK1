using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Reflection;

namespace ECK1.CommonUtils.Swagger;

public class RequirePermissionOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var permissions = context.MethodInfo
            .GetCustomAttributes<RequirePermissionAttribute>(true)
            .Select(a => a.Permission)
            .ToList();

        if (permissions.Count == 0)
            return;

        var permissionsArray = new OpenApiArray();
        permissionsArray.AddRange(permissions.Select(p => (IOpenApiAny)new OpenApiString(p)));
        operation.Extensions["x-required-permissions"] = permissionsArray;
    }
}
