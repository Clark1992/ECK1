using ECK1.Gateway.Commands;
using ECK1.Gateway.Swagger;

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

        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/merged/swagger.json", "All Services (Merged)");
            options.RoutePrefix = "swagger";
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
