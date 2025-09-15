namespace ECK1.CommandsAPI.Utils;

public static class TypeUtils
{
    public static Dictionary<Type, Type> GetEventTypeMapping<TSourceInterface, TDestinationInterface>()
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
                result[sourceType] = destinationType;
            }
        }

        return result;
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
