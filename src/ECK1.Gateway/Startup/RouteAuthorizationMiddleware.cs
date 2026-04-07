using ECK1.Gateway.Commands;
using Microsoft.Extensions.Options;

namespace ECK1.Gateway.Startup;

public class RouteAuthorizationMiddleware(
    RequestDelegate next,
    RouteAuthorizationState authState,
    IOptions<ZitadelConfig> zitadelOptions)
{
    private static readonly string[] ExcludedPrefixes = ["/swagger", "/health"];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;
        var authEnabled = !string.IsNullOrEmpty(zitadelOptions.Value.Authority);

        // When Zitadel is configured, require authentication for all API routes
        if (authEnabled && !IsExcludedPath(path)
            && context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Authentication required" });
            return;
        }

        // Check specific permissions (additional authorization layer)
        List<string> requiredPermissions = null;

        // For dynamic command endpoints, check metadata directly
        var commandEntry = context.GetEndpoint()?.Metadata.GetMetadata<CommandRouteEntry>();
        if (commandEntry is { RequiredPermissions.Count: > 0 })
        {
            requiredPermissions = commandEntry.RequiredPermissions;
        }
        else
        {
            // For proxied sync endpoints, check the authorization state
            var method = context.Request.Method;
            if (path is not null)
                requiredPermissions = authState.GetRequiredPermissions(method, path);
        }

        if (requiredPermissions is { Count: > 0 })
        {
            var userPermissions = context.User.FindAll("permission").Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!requiredPermissions.Any(p => userPermissions.Contains(p)))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = $"One of the following permissions is required: {string.Join(", ", requiredPermissions)}"
                });
                return;
            }
        }

        await next(context);
    }

    private static bool IsExcludedPath(string path) =>
        path is null || ExcludedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
