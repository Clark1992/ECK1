using System.Text.Json;
using ECK1.AsyncApi.Document;

namespace ECK1.Gateway.Commands;

/// <summary>
/// Binds HTTP request data into a JSON command object based on the async API property descriptors.
/// Supports binding from: body, route, query, claim, header.
/// </summary>
public class HttpRequestCommandBinder
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task<BindingResult> BindAsync(
        HttpContext context,
        CommandRouteEntry entry,
        Dictionary<string, string> routeValues)
    {
        JsonElement? bodyJson;
        try
        {
            bodyJson = await ParseRequestBodyAsync(context);
        }
        catch (JsonException)
        {
            return BindingResult.Failure("Invalid JSON in request body.");
        }

        var result = BindProperties(context, entry, routeValues, bodyJson);
        var commandJson = JsonSerializer.Serialize(result, SerializerOptions);
        var messageKey = DetermineMessageKey(result, entry);

        return BindingResult.Success(commandJson, messageKey);
    }

    private static async Task<JsonElement?> ParseRequestBodyAsync(HttpContext context)
    {
        if (context.Request.ContentLength is null or 0 && context.Request.ContentType is null)
            return null;

        context.Request.EnableBuffering();
        return await JsonSerializer.DeserializeAsync<JsonElement>(
            context.Request.Body, cancellationToken: context.RequestAborted);
    }

    private static Dictionary<string, object> BindProperties(
        HttpContext context,
        CommandRouteEntry entry,
        Dictionary<string, string> routeValues,
        JsonElement? bodyJson)
    {
        var result = new Dictionary<string, object>
        {
            ["$type"] = entry.CommandName
        };

        foreach (var prop in entry.Properties)
        {
            var value = BindPropertyValue(context, prop, routeValues, bodyJson);
            if (value is not null)
                result[ToCamelCase(prop.Name)] = value;
        }

        return result;
    }

    private static object BindPropertyValue(
        HttpContext context,
        AsyncApiPropertyDescriptor prop,
        Dictionary<string, string> routeValues,
        JsonElement? bodyJson) => prop.Source switch
    {
        "route" => BindFromRoute(prop, routeValues),
        "query" => BindFromQuery(context, prop),
        "claim" => BindFromClaim(context, prop),
        "header" => BindFromHeader(context, prop),
        _ => BindFromBody(prop, bodyJson)
    };

    private static object BindFromRoute(AsyncApiPropertyDescriptor prop, Dictionary<string, string> routeValues)
    {
        var sourceName = prop.SourceName ?? prop.Name;
        return routeValues.TryGetValue(sourceName, out var routeVal)
            ? ConvertRouteValue(routeVal, prop.TypeName)
            : null;
    }

    private static object BindFromQuery(HttpContext context, AsyncApiPropertyDescriptor prop)
    {
        var sourceName = prop.SourceName ?? prop.Name;
        return context.Request.Query.TryGetValue(sourceName, out var queryVal)
            ? ConvertRouteValue(queryVal.ToString(), prop.TypeName)
            : null;
    }

    private static object BindFromClaim(HttpContext context, AsyncApiPropertyDescriptor prop)
    {
        if (prop.SourceName is null || context.User?.Identity?.IsAuthenticated != true)
            return null;

        var claim = context.User.FindFirst(prop.SourceName);
        return claim is not null ? ConvertRouteValue(claim.Value, prop.TypeName) : null;
    }

    private static object BindFromHeader(HttpContext context, AsyncApiPropertyDescriptor prop)
    {
        var sourceName = prop.SourceName ?? prop.Name;
        return context.Request.Headers.TryGetValue(sourceName, out var headerVal)
            ? headerVal.ToString()
            : null;
    }

    private static object BindFromBody(AsyncApiPropertyDescriptor prop, JsonElement? bodyJson)
    {
        if (!bodyJson.HasValue)
            return null;

        var sourceName = prop.SourceName ?? prop.Name;
        return ExtractJsonProperty(bodyJson.Value, sourceName);
    }

    private static string DetermineMessageKey(Dictionary<string, object> result, CommandRouteEntry entry)
    {
        if (entry.KeyProperty is not null)
        {
            var keyPropName = ToCamelCase(entry.KeyProperty);
            if (result.TryGetValue(keyPropName, out var keyVal) && keyVal is not null)
                return keyVal.ToString();
        }

        return Guid.NewGuid().ToString();
    }

    private static JsonElement? ExtractJsonProperty(JsonElement element, string propertyName)
    {
        if (element.TryGetProperty(propertyName, out var val))
            return val;

        var camelCase = ToCamelCase(propertyName);
        if (element.TryGetProperty(camelCase, out val))
            return val;

        var pascalCase = ToPascalCase(propertyName);
        if (element.TryGetProperty(pascalCase, out val))
            return val;

        return null;
    }

    private static object ConvertRouteValue(string value, string typeName) => typeName switch
    {
        "guid" => Guid.TryParse(value, out var g) ? g : value,
        "int" => int.TryParse(value, out var i) ? i : value,
        "long" => long.TryParse(value, out var l) ? l : value,
        "double" => double.TryParse(value, out var d) ? d : value,
        "bool" => bool.TryParse(value, out var b) ? b : value,
        _ => value
    };

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToLowerInvariant(s[0]) + s[1..];
    }

    private static string ToPascalCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}

public class BindingResult
{
    public bool IsSuccess { get; private init; }
    public string CommandJson { get; private init; }
    public string MessageKey { get; private init; }
    public string Error { get; private init; }

    public static BindingResult Success(string json, string key) =>
        new() { IsSuccess = true, CommandJson = json, MessageKey = key };

    public static BindingResult Failure(string error) =>
        new() { IsSuccess = false, Error = error };
}
