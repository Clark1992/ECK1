using System.Text.Json;
using System.Text.Json.Nodes;
using ECK1.AsyncApi.Document;
using ECK1.Gateway.Commands;
using ECK1.Gateway.Proxy;

namespace ECK1.Gateway.Swagger;

/// <summary>
/// Aggregates OpenAPI documents from discovered services, rewrites paths
/// with service prefixes, and serves them via custom endpoints.
/// </summary>
public class SwaggerAggregator(
    ServiceRouteState state,
    CommandRouteState commandState,
    ILogger<SwaggerAggregator> logger)
{

    /// <summary>
    /// Returns a list of available swagger spec names (service names).
    /// </summary>
    public IReadOnlyList<string> GetAvailableSpecs()
    {
        return [.. state.SwaggerDocs.Keys];
    }

    /// <summary>
    /// Gets the rewritten OpenAPI document for a specific service, with paths
    /// prefixed by the service name.
    /// </summary>
    public string GetRewrittenSwaggerJson(string serviceName)
    {
        if (!state.SwaggerDocs.TryGetValue(serviceName, out var doc))
            return null;

        try
        {
            var node = JsonNode.Parse(doc.RootElement.GetRawText());
            if (node is not JsonObject root)
                return null;

            RewritePaths(root, serviceName);
            RewriteServers(root, serviceName);

            return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to rewrite swagger for service {Service}", serviceName);
            return null;
        }
    }

    /// <summary>
    /// Builds a merged OpenAPI document containing all services.
    /// </summary>
    public string GetMergedSwaggerJson()
    {
        var merged = new JsonObject
        {
            ["openapi"] = "3.0.1",
            ["info"] = new JsonObject
            {
                ["title"] = "ECK1 API Gateway",
                ["version"] = "1.0.0",
                ["description"] = "Aggregated API documentation from all discovered microservices."
            },
            ["paths"] = new JsonObject(),
            ["components"] = new JsonObject
            {
                ["schemas"] = new JsonObject()
            }
        };

        var mergedPaths = merged["paths"]!.AsObject();
        var mergedSchemas = merged["components"]!["schemas"]!.AsObject();

        foreach (var (serviceName, doc) in state.SwaggerDocs)
        {
            try
            {
                var node = JsonNode.Parse(doc.RootElement.GetRawText());
                if (node is not JsonObject serviceDoc)
                    continue;

                MergePathsWithPrefix(mergedPaths, serviceDoc, serviceName);
                MergeSchemas(mergedSchemas, serviceDoc, serviceName);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to merge swagger for service {Service}", serviceName);
            }
        }

        // Add async command routes from discovered AsyncAPI documents
        AddCommandRoutes(mergedPaths);

        return merged.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static void RewritePaths(JsonObject root, string serviceName)
    {
        if (root["paths"] is not JsonObject paths)
            return;

        var newPaths = new JsonObject();
        foreach (var (path, value) in paths)
        {
            var prefixedPath = $"/{serviceName}{path}";
            newPaths[prefixedPath] = value?.DeepClone();
        }

        root["paths"] = newPaths;
    }

    private static void RewriteServers(JsonObject root, string serviceName)
    {
        root["servers"] = new JsonArray(new JsonObject
        {
            ["url"] = "/",
            ["description"] = $"Gateway proxy for {serviceName}"
        });
    }

    private static void MergePathsWithPrefix(JsonObject mergedPaths, JsonObject serviceDoc, string serviceName)
    {
        if (serviceDoc["paths"] is not JsonObject paths)
            return;

        foreach (var (path, value) in paths)
        {
            var prefixedPath = $"/{serviceName}{path}";

            // Add tags to all operations for grouping by service
            if (value is JsonObject pathItem)
            {
                var taggedItem = pathItem.DeepClone().AsObject();
                RewriteSchemaRefs(taggedItem, serviceName);
                foreach (var (_, operationNode) in taggedItem)
                {
                    if (operationNode is JsonObject operation)
                    {
                        operation["tags"] = new JsonArray(JsonValue.Create(serviceName));
                    }
                }
                mergedPaths[prefixedPath] = taggedItem;
            }
            else
            {
                mergedPaths[prefixedPath] = value?.DeepClone();
            }
        }
    }

    private static void MergeSchemas(JsonObject mergedSchemas, JsonObject serviceDoc, string serviceName)
    {
        if (serviceDoc["components"] is not JsonObject components)
            return;
        if (components["schemas"] is not JsonObject schemas)
            return;

        foreach (var (schemaName, schema) in schemas)
        {
            // Prefix schema names to avoid collisions
            var prefixedName = $"{serviceName}.{schemaName}";
            var cloned = schema?.DeepClone();
            if (cloned is not null)
                RewriteSchemaRefs(cloned, serviceName);
            mergedSchemas[prefixedName] = cloned;
        }
    }

    /// <summary>
    /// Recursively rewrites all $ref pointers in a JSON node to include the service name prefix.
    /// e.g. #/components/schemas/Foo → #/components/schemas/serviceName.Foo
    /// </summary>
    private static void RewriteSchemaRefs(JsonNode node, string serviceName)
    {
        const string schemasPrefix = "#/components/schemas/";

        if (node is JsonObject obj)
        {
            if (obj.TryGetPropertyValue("$ref", out var refNode) &&
                refNode?.GetValue<string>() is { } refStr &&
                refStr.StartsWith(schemasPrefix))
            {
                obj["$ref"] = $"{schemasPrefix}{serviceName}.{refStr[schemasPrefix.Length..]}";
            }

            foreach (var child in obj.Select(kvp => kvp.Value).ToList())
            {
                if (child is not null)
                    RewriteSchemaRefs(child, serviceName);
            }
        }
        else if (node is JsonArray arr)
        {
            foreach (var item in arr)
            {
                if (item is not null)
                    RewriteSchemaRefs(item, serviceName);
            }
        }
    }

    private void AddCommandRoutes(JsonObject mergedPaths)
    {
        foreach (var (_, entry) in commandState.Routes)
        {
            var method = entry.Method.ToLowerInvariant();
            var path = entry.FullRoutePattern;

            var parameters = new JsonArray();
            var bodyProperties = new JsonObject();
            var requiredProps = new JsonArray();

            foreach (var prop in entry.Properties)
            {
                if (prop.Source is "route" or "query")
                {
                    parameters.Add(new JsonObject
                    {
                        ["name"] = prop.SourceName ?? ToCamelCase(prop.Name),
                        ["in"] = prop.Source == "route" ? "path" : "query",
                        ["required"] = prop.Source == "route",
                        ["schema"] = new JsonObject { ["type"] = MapToOpenApiType(prop.TypeName) }
                    });
                }
                else
                {
                    bodyProperties[ToCamelCase(prop.Name)] = BuildPropertySchema(prop);
                    if (!prop.IsNullable)
                        requiredProps.Add(JsonValue.Create(ToCamelCase(prop.Name)));
                }
            }

            var operation = new JsonObject
            {
                ["tags"] = new JsonArray(JsonValue.Create($"{entry.ServiceName} (async)")),
                ["summary"] = entry.CommandName,
                ["operationId"] = entry.CommandName,
                ["responses"] = new JsonObject
                {
                    ["202"] = new JsonObject { ["description"] = "Command accepted" },
                    ["400"] = new JsonObject { ["description"] = "Bad request" },
                    ["502"] = new JsonObject { ["description"] = "Failed to publish" }
                }
            };

            if (parameters.Count > 0)
                operation["parameters"] = parameters;

            if (bodyProperties.Count > 0)
            {
                var bodySchema = new JsonObject
                {
                    ["type"] = "object",
                    ["properties"] = bodyProperties
                };
                if (requiredProps.Count > 0)
                    bodySchema["required"] = requiredProps;

                operation["requestBody"] = new JsonObject
                {
                    ["required"] = true,
                    ["content"] = new JsonObject
                    {
                        ["application/json"] = new JsonObject
                        {
                            ["schema"] = bodySchema
                        }
                    }
                };
            }

            if (mergedPaths[path] is not JsonObject pathItem)
            {
                pathItem = new JsonObject();
                mergedPaths[path] = pathItem;
            }
            pathItem[method] = operation;
        }
    }

    private static JsonObject BuildPropertySchema(AsyncApiPropertyDescriptor prop)
    {
        if (prop.IsCollection)
        {
            var itemsSchema = prop.Properties is { Count: > 0 }
                ? BuildObjectSchema(prop.Properties)
                : new JsonObject { ["type"] = MapToOpenApiType(prop.TypeName[6..^1]) };

            return new JsonObject
            {
                ["type"] = "array",
                ["items"] = itemsSchema
            };
        }

        if (prop.IsComplex && prop.Properties is { Count: > 0 })
            return BuildObjectSchema(prop.Properties);

        return new JsonObject { ["type"] = MapToOpenApiType(prop.TypeName) };
    }

    private static JsonObject BuildObjectSchema(List<AsyncApiPropertyDescriptor> properties)
    {
        var props = new JsonObject();
        foreach (var p in properties)
            props[ToCamelCase(p.Name)] = BuildPropertySchema(p);

        return new JsonObject
        {
            ["type"] = "object",
            ["properties"] = props
        };
    }

    private static string MapToOpenApiType(string typeName) => typeName switch
    {
        "guid" or "string" => "string",
        "int" or "long" => "integer",
        "double" or "decimal" => "number",
        "bool" => "boolean",
        _ when typeName.StartsWith("array<") => "array",
        _ => "object"
    };

    private static string ToCamelCase(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToLowerInvariant(s[0]) + s[1..];
    }
}
