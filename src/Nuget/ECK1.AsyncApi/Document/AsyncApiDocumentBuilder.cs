using System.Reflection;
using System.Text.RegularExpressions;
using ECK1.AsyncApi.Attributes;
using Microsoft.Extensions.Configuration;

namespace ECK1.AsyncApi.Document;

public class AsyncApiDocumentBuilder
{
    private readonly IConfiguration _configuration;
    private readonly string _serviceName;

    public AsyncApiDocumentBuilder(IConfiguration configuration, string serviceName)
    {
        _configuration = configuration;
        _serviceName = serviceName;
    }

    public AsyncApiDocument Build(params Assembly[] assemblies)
    {
        var document = new AsyncApiDocument { ServiceName = _serviceName };

        foreach (var assembly in assemblies)
        {
            var commandInterfaces = assembly.GetTypes()
                .Where(t => t.IsInterface && t.GetCustomAttribute<CommandAttribute>() is not null);

            foreach (var iface in commandInterfaces)
            {
                var topicAttr = iface.GetCustomAttribute<TopicAttribute>();
                if (topicAttr is null) continue;

                string topic = ResolveTopicName(topicAttr);
                if (string.IsNullOrEmpty(topic)) continue;

                var concreteTypes = assembly.GetTypes()
                    .Where(t => t is { IsAbstract: false, IsInterface: false }
                                && iface.IsAssignableFrom(t)
                                && t.GetCustomAttribute<RouteAttribute>() is not null);

                foreach (var type in concreteTypes)
                {
                    var descriptor = BuildCommandDescriptor(type, topic);
                    if (descriptor is not null)
                        document.Commands.Add(descriptor);
                }
            }
        }

        return document;
    }

    private static AsyncApiCommandDescriptor BuildCommandDescriptor(Type type, string topic)
    {
        var routeAttr = type.GetCustomAttribute<RouteAttribute>();
        if (routeAttr is null) return null;

        var routeParams = ExtractRouteParameters(routeAttr.Route);

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Select(p => BuildPropertyDescriptor(p, routeParams))
            .ToList();

        string keyProperty = properties.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))?.Name;

        return new AsyncApiCommandDescriptor
        {
            Name = type.Name,
            Method = routeAttr.Method,
            Route = routeAttr.Route,
            Topic = topic,
            KeyProperty = keyProperty,
            Properties = properties
        };
    }

    private static AsyncApiPropertyDescriptor BuildPropertyDescriptor(PropertyInfo prop, HashSet<string> routeParams)
    {
        var actualType = Nullable.GetUnderlyingType(prop.PropertyType) ?? prop.PropertyType;

        var descriptor = new AsyncApiPropertyDescriptor
        {
            Name = prop.Name,
            TypeName = GetFriendlyTypeName(prop.PropertyType),
            IsNullable = IsNullableType(prop.PropertyType),
            IsComplex = IsComplexType(prop.PropertyType),
            IsCollection = IsCollectionType(prop.PropertyType)
        };

        if (descriptor.IsComplex)
        {
            descriptor.Properties = BuildNestedProperties(actualType, [actualType]);
        }
        else if (descriptor.IsCollection)
        {
            var elementType = actualType.IsArray
                ? actualType.GetElementType()!
                : actualType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
            if (IsComplexType(elementType))
                descriptor.Properties = BuildNestedProperties(elementType, [elementType]);
        }

        if (prop.GetCustomAttribute<FromRouteAttribute>() is { } fromRoute)
        {
            descriptor.Source = "route";
            descriptor.SourceName = fromRoute.Name ?? prop.Name;
        }
        else if (prop.GetCustomAttribute<FromQueryAttribute>() is { } fromQuery)
        {
            descriptor.Source = "query";
            descriptor.SourceName = fromQuery.Name ?? prop.Name;
        }
        else if (prop.GetCustomAttribute<FromClaimAttribute>() is { } fromClaim)
        {
            descriptor.Source = "claim";
            descriptor.SourceName = fromClaim.Name;
        }
        else if (prop.GetCustomAttribute<FromHeaderAttribute>() is { } fromHeader)
        {
            descriptor.Source = "header";
            descriptor.SourceName = fromHeader.Name ?? prop.Name;
        }
        else if (prop.GetCustomAttribute<FromBodyAttribute>() is { } fromBody)
        {
            descriptor.Source = "body";
            descriptor.SourceName = fromBody.Name;
        }
        else if (routeParams.Contains(prop.Name.ToLowerInvariant()))
        {
            // Convention: if property name matches a route parameter, bind from route
            descriptor.Source = "route";
            descriptor.SourceName = prop.Name;
        }
        else
        {
            descriptor.Source = "body";
        }

        return descriptor;
    }

    private static HashSet<string> ExtractRouteParameters(string routeTemplate)
    {
        var matches = Regex.Matches(routeTemplate, @"\{(\w+)\}");
        return [.. matches.Select(m => m.Groups[1].Value.ToLowerInvariant())];
    }

    private string ResolveTopicName(TopicAttribute attr)
    {
        if (!string.IsNullOrEmpty(attr.Topic))
            return attr.Topic;

        if (!string.IsNullOrEmpty(attr.TopicConfigKey))
            return _configuration[attr.TopicConfigKey];

        return null;
    }

    private static string GetFriendlyTypeName(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string)) return "string";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(double)) return "double";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(Guid)) return "guid";
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return "datetime";

        if (IsCollectionType(type))
        {
            var elementType = type.IsArray
                ? type.GetElementType()!
                : type.GetGenericArguments().FirstOrDefault() ?? typeof(object);
            return $"array<{GetFriendlyTypeName(elementType)}>";
        }

        return type.Name;
    }

    private static List<AsyncApiPropertyDescriptor> BuildNestedProperties(Type type, HashSet<Type> visited)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead)
            .Select(p =>
            {
                var propType = Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType;
                var desc = new AsyncApiPropertyDescriptor
                {
                    Name = p.Name,
                    TypeName = GetFriendlyTypeName(p.PropertyType),
                    IsNullable = IsNullableType(p.PropertyType),
                    IsComplex = IsComplexType(p.PropertyType),
                    IsCollection = IsCollectionType(p.PropertyType),
                    Source = "body"
                };

                if (desc.IsComplex && !visited.Contains(propType))
                {
                    visited.Add(propType);
                    desc.Properties = BuildNestedProperties(propType, visited);
                }
                else if (desc.IsCollection)
                {
                    var elementType = propType.IsArray
                        ? propType.GetElementType()!
                        : propType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
                    if (IsComplexType(elementType) && !visited.Contains(elementType))
                    {
                        visited.Add(elementType);
                        desc.Properties = BuildNestedProperties(elementType, visited);
                    }
                }

                return desc;
            })
            .ToList();
    }

    private static bool IsNullableType(Type type) =>
        !type.IsValueType || Nullable.GetUnderlyingType(type) is not null;

    private static bool IsComplexType(Type type)
    {
        type = Nullable.GetUnderlyingType(type) ?? type;
        if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)
            || type == typeof(Guid) || type == typeof(DateTime) || type == typeof(DateTimeOffset))
            return false;
        if (type.IsEnum) return false;
        if (IsCollectionType(type)) return false;
        return true;
    }

    private static bool IsCollectionType(Type type) =>
        type != typeof(string) && (
            type.IsArray ||
            (type.IsGenericType && type.GetGenericTypeDefinition() is var gd &&
             (gd == typeof(List<>) || gd == typeof(IList<>) ||
              gd == typeof(ICollection<>) || gd == typeof(IEnumerable<>) ||
              gd == typeof(IReadOnlyList<>) || gd == typeof(IReadOnlyCollection<>)))
        );
}
