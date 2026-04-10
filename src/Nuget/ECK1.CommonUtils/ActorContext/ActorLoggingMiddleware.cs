using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace ECK1.CommonUtils.ActorContext;

public class ActorLoggingMiddleware(RequestDelegate next, ILogger<ActorLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var actorId = Activity.Current?.GetBaggageItem("actor_id") ?? "system";
        var actorName = Activity.Current?.GetBaggageItem("actor_name") ?? "system";

        using (logger.BeginScope(new Dictionary<string, object>
        {
            ["actor_id"] = actorId,
            ["actor_name"] = actorName
        }))
        {
            await next(context);
        }
    }
}

public static class ActorLoggingExtensions
{
    public static IApplicationBuilder UseActorLogging(this IApplicationBuilder app) =>
        app.UseMiddleware<ActorLoggingMiddleware>();
}
