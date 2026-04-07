using ECK1.Gateway.Commands;
using ECK1.Gateway.Proxy;
using ECK1.Gateway.Swagger;
using Microsoft.Extensions.Options;

namespace ECK1.Gateway.Startup;

public static class GatewayAppExtensions
{
    public static WebApplication MapSwaggerEndpoints(this WebApplication app)
    {
        app.MapGet("/swagger/services", (SwaggerAggregator aggregator) =>
        {
            var specs = aggregator.GetAvailableSpecs();
            return Results.Ok(specs.Select(s => new
            {
                name = s,
                url = $"/swagger/{s}/swagger.json"
            }));
        }).ExcludeFromDescription();

        app.MapGet("/swagger/{serviceName}/swagger.json",
            (string serviceName, SwaggerAggregator aggregator) =>
        {
            var json = aggregator.GetRewrittenSwaggerJson(serviceName);
            return json is not null
                ? Results.Content(json, "application/json")
                : Results.NotFound();
        }).ExcludeFromDescription();

        app.MapGet("/swagger/merged/swagger.json", (SwaggerAggregator aggregator) =>
        {
            var json = aggregator.GetMergedSwaggerJson();
            return Results.Content(json, "application/json");
        }).ExcludeFromDescription();

        // Dynamic config endpoint for Swagger UI — returns per-service specs
        // so the dropdown is populated with all discovered services.
        app.MapGet("/swagger/config.json", (ServiceRouteState state, SwaggerAggregator aggregator) =>
        {
            var available = aggregator.GetAvailableSpecs();
            var urls = new List<object>
            {
                new { url = "/swagger/merged/swagger.json", name = "All Services (Merged)" }
            };

            foreach (var serviceName in available)
            {
                urls.Add(new { url = $"/swagger/{serviceName}/swagger.json", name = serviceName });
            }

            return Results.Json(new { urls });
        }).ExcludeFromDescription();

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/merged/swagger.json", "All Services (Merged)");
            options.RoutePrefix = "swagger";
            options.EnablePersistAuthorization();

            var zitadel = app.Services.GetRequiredService<IOptions<ZitadelConfig>>().Value;
            if (!string.IsNullOrEmpty(zitadel.ClientId))
            {
                options.OAuthClientId(zitadel.ClientId);
                options.OAuthUsePkce();
                options.OAuthScopes("openid", "profile", "email");
            }
        });

        return app;
    }

    public static WebApplication MapGatewayEndpoints(this WebApplication app)
    {
        app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

        var commandDataSource = app.Services.GetRequiredService<DynamicCommandEndpointDataSource>();
        ((IEndpointRouteBuilder)app).DataSources.Add(commandDataSource);

        app.MapReverseProxy();

        return app;
    }
}
