namespace ECK1.Gateway.Startup;

public class UserContextMiddleware(RequestDelegate next)
{
    public static class Headers
    {
        public const string UserId = "X-User-Id";
        public const string UserName = "X-User-Name";
        public const string UserEmail = "X-User-Email";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub = context.User.FindFirst("sub")?.Value;
            var name = context.User.FindFirst("preferred_username")?.Value
                       ?? context.User.FindFirst("name")?.Value;
            var email = context.User.FindFirst("email")?.Value;

            if (sub is not null)
                context.Request.Headers[Headers.UserId] = sub;
            if (name is not null)
                context.Request.Headers[Headers.UserName] = name;
            if (email is not null)
                context.Request.Headers[Headers.UserEmail] = email;
        }

        await next(context);
    }
}
