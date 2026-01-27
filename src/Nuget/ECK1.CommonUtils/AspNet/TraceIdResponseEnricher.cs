using Microsoft.AspNetCore.Builder;
using System.Diagnostics;

namespace ECK1.CommonUtils.AspNet;

public static class TraceIdResponseEnricher
{
    public static IApplicationBuilder UseTraceResponseEnricher(
        this IApplicationBuilder app,
        string traceParentHeaderName = "traceparent")
    {
        return app.Use((context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                var activity = Activity.Current;
                if (activity is not null)
                {
                    if (!string.IsNullOrWhiteSpace(activity.Id))
                    {
                        context.Response.Headers.TryAdd(traceParentHeaderName, activity.Id);
                    }
                }

                return Task.CompletedTask;
            });

            return next();
        });
    }
}
