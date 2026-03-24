using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using ECK1.AsyncApi.Document;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;

namespace ECK1.AsyncApi.Extensions;

public static class AsyncApiEndpointExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public static IEndpointRouteBuilder MapAsyncApiDocument(
        this IEndpointRouteBuilder endpoints,
        string serviceName,
        params Assembly[] assemblies)
    {
        endpoints.MapGet("/.well-known/async-api.json", (IConfiguration config) =>
        {
            var builder = new AsyncApiDocumentBuilder(config, serviceName);
            var document = builder.Build(assemblies);
            return Results.Json(document, JsonOptions);
        }).ExcludeFromDescription();

        return endpoints;
    }
}
