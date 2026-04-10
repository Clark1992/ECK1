namespace ECK1.Gateway.Startup;

public class UserContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirst("sub")?.Value ?? "system";
            var name = context.User.FindFirst("preferred_username")?.Value
                    ?? context.User.FindFirst("name")?.Value
                    ?? "system";

            System.Diagnostics.Activity.Current?.SetBaggage("actor_id", sub);
            System.Diagnostics.Activity.Current?.SetBaggage("actor_name", name);
        }

        await next(context);
    }
}
