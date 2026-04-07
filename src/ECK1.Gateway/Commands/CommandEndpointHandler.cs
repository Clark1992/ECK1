using ECK1.Kafka;
using Newtonsoft.Json.Linq;
using Confluent.Kafka;
using System.Text;
using ECK1.Gateway.Startup;

namespace ECK1.Gateway.Commands;

/// <summary>
/// Handles HTTP requests matched to async command endpoints.
/// Binds request data into a command JSON and publishes to Kafka.
/// </summary>
public class CommandEndpointHandler(
    HttpRequestCommandBinder binder,
    IKafkaProducer<JObject> producer,
    ILogger<CommandEndpointHandler> logger)
{
    public async Task HandleAsync(HttpContext context)
    {
        var entry = context.GetEndpoint()?.Metadata.GetMetadata<CommandRouteEntry>();
        if (entry is null)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { error = "No command route metadata found." });
            return;
        }

        var routeValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in context.Request.RouteValues)
        {
            if (value is not null)
                routeValues[key] = value.ToString()!;
        }

        logger.LogInformation("Matched command route: {Method} {Path} → {Command} on topic {Topic}",
            context.Request.Method, context.Request.Path.Value, entry.CommandName, entry.Topic);

        var bindingResult = await binder.BindAsync(context, entry, routeValues);

        if (!bindingResult.IsSuccess)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new { error = bindingResult.Error });
            return;
        }

        try
        {
            var payload = JObject.Parse(bindingResult.CommandJson);
            var kafkaHeaders = BuildUserHeaders(context);

            await producer.ProduceAsync(
                entry.Topic, payload, bindingResult.MessageKey, kafkaHeaders, context.RequestAborted);

            context.Response.StatusCode = StatusCodes.Status202Accepted;
            await context.Response.WriteAsJsonAsync(new
            {
                status = "accepted",
                command = entry.CommandName,
                topic = entry.Topic,
                key = bindingResult.MessageKey
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish command {Command} to topic {Topic}",
                entry.CommandName, entry.Topic);

            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            await context.Response.WriteAsJsonAsync(new { error = $"Failed to publish command: {ex.Message}" });
        }
    }

    private static Headers BuildUserHeaders(HttpContext context)
    {
        var headers = new Headers();

        AddHeaderIfPresent(context, headers, UserContextMiddleware.Headers.UserId);
        AddHeaderIfPresent(context, headers, UserContextMiddleware.Headers.UserName);
        AddHeaderIfPresent(context, headers, UserContextMiddleware.Headers.UserEmail);

        return headers.Count > 0 ? headers : null;
    }

    private static void AddHeaderIfPresent(HttpContext context, Headers kafkaHeaders, string headerName)
    {
        if (context.Request.Headers.TryGetValue(headerName, out var value)
            && !string.IsNullOrEmpty(value))
        {
            kafkaHeaders.Add(headerName, Encoding.UTF8.GetBytes(value.ToString()));
        }
    }
}
