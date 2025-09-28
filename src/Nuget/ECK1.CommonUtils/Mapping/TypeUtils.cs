using System.Reflection;

namespace ECK1.CommonUtils.Mapping;

public static class TypeUtils
{
    public static Dictionary<Type, Type> GetTypesMapping<TSourceInterface, TDestinationInterface>()
    {
        Dictionary<Type, Type> result = new();

        GetMappingImpl<TSourceInterface, TDestinationInterface>(t => { result[t.src] = t.dst; });

        return result;
    }

    public static Dictionary<Type, Type> GetTypesMappingWithNested<TSourceInterface, TDestinationInterface>()
    {
        Dictionary<Type, Type> result = new();

        GetMappingImpl<TSourceInterface, TDestinationInterface>(
            t => { AddWithNestedProperties(result, t.src, t.dst, t.allDstTypes); });

        return result;
    }

    private static Dictionary<Type, Type> GetMappingImpl<TSourceInterface, TDestinationInterface>(
        Action<(Type src, Type dst, Dictionary<string, Type> allDstTypes)> mappingAction)
    {
        var sourceTypes = typeof(TSourceInterface).Assembly.GetTypes()
            .Where(t =>
                t.IsClass &&
                !t.IsAbstract &&
                typeof(TSourceInterface).IsAssignableFrom(t))
            .ToList();

        var destinationTypes = typeof(TDestinationInterface).Assembly.GetTypes()
            .Where(t =>
                t.IsClass &&
                !t.IsAbstract &&
                typeof(TDestinationInterface).IsAssignableFrom(t))
            .ToDictionary(t => t.Name, t => t);

        Dictionary<Type, Type> result = new();
        foreach (var sourceType in sourceTypes)
        {
            if (destinationTypes.TryGetValue(sourceType.Name, out var destinationType))
            {
                mappingAction((sourceType, destinationType, destinationTypes));
            }
        }

        return result;
    }

    private static void AddWithNestedProperties(
        Dictionary<Type, Type> result,
        Type sourceType,
        Type destType,
        Dictionary<string, Type> allDestTypes)
    {
        if (!result.ContainsKey(sourceType))
            result[sourceType] = destType;

        var sourceProps = sourceType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => IsCustomType(p.PropertyType));

        var destProps = destType
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => IsCustomType(p.PropertyType))
            .ToDictionary(p => p.Name, p => p);

        foreach (var sProp in sourceProps)
        {
            if (!destProps.TryGetValue(sProp.Name, out var dProp))
                continue;

            if (sProp.PropertyType.Name != dProp.PropertyType.Name)
                continue;

            if (result.ContainsKey(sProp.PropertyType))
                continue;

            if (!allDestTypes.ContainsKey(dProp.PropertyType.Name))
                allDestTypes[dProp.PropertyType.Name] = dProp.PropertyType;

            AddWithNestedProperties(result, sProp.PropertyType, dProp.PropertyType, allDestTypes);
        }
    }

    private static bool IsCustomType(Type type)
    {
        return type.IsClass &&
               type != typeof(string) &&
               !type.Namespace.StartsWith("System");
    }

    public static bool IsSubclassOfGeneric(Type type, Type genericBase)
    {
        while (type != null && type != typeof(object))
        {
            var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            if (cur == genericBase) return true;
            type = type.BaseType;
        }
        return false;
    }

    public static Type GetGenericBaseType(Type type, Type genericBase)
    {
        while (type != null && type != typeof(object))
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == genericBase)
                return type;
            type = type.BaseType;
        }

        throw new InvalidOperationException($"Type {type} is not derived from {genericBase}");
    }
}
