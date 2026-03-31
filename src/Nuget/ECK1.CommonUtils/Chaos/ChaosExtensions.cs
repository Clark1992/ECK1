using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ECK1.CommonUtils.Chaos;

public static class ChaosExtensions
{
    public static IServiceCollection AddChaosEngine(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ChaosConfig.Section);
        services.Configure<ChaosConfig>(section);

        var config = section.Get<ChaosConfig>();
        if (config?.Enabled == true)
            services.AddSingleton<IChaosEngine, ActiveChaosEngine>();
        else
            services.AddSingleton<IChaosEngine, NullChaosEngine>();

        return services;
    }

    public static IEndpointRouteBuilder MapChaosEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/chaos/status", (IChaosEngine chaos) =>
            Results.Ok(new
            {
                Type = chaos.GetType().Name,
                Active = chaos.GetActiveScenarios()
            }));

        app.MapPost("/chaos/activate/{scenarioId}", (IChaosEngine chaos, string scenarioId) =>
        {
            chaos.Activate(scenarioId);
            return Results.Ok(new { Activated = scenarioId, Active = chaos.GetActiveScenarios() });
        });

        app.MapDelete("/chaos/activate/{scenarioId}", (IChaosEngine chaos, string scenarioId) =>
        {
            chaos.Deactivate(scenarioId);
            return Results.Ok(new { Deactivated = scenarioId, Active = chaos.GetActiveScenarios() });
        });

        app.MapDelete("/chaos", (IChaosEngine chaos) =>
        {
            chaos.DeactivateAll();
            return Results.Ok(new { Message = "All scenarios deactivated" });
        });

        return app;
    }
}
