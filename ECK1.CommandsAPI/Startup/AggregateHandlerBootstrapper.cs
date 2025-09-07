using ECK1.CommandsAPI.Domain;
using System.Reflection;
using System.Runtime.CompilerServices;


namespace ECK1.CommandsAPI.Startup;

public static class AggregateHandlerBootstrapper
{
    public static void Initialize(params Assembly[] assembliesToScan)
    {
        var aggregateTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.BaseType != null && IsSubclassOfGeneric(t, typeof(AggregateRoot<>)))
            .ToList();

        foreach (var aggregateType in aggregateTypes)
        {
            var baseType = GetGenericBaseType(aggregateType, typeof(AggregateRoot<>));
            var eventType = baseType.GetGenericArguments()[0];

            var factoryType = typeof(AggregateFactory<,>).MakeGenericType(aggregateType, eventType);
            RuntimeHelpers.RunClassConstructor(factoryType.TypeHandle);

            RuntimeHelpers.RunClassConstructor(aggregateType.TypeHandle);
        }
    }

    private static bool IsSubclassOfGeneric(Type type, Type genericBase)
    {
        while (type != null && type != typeof(object))
        {
            var cur = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
            if (cur == genericBase) return true;
            type = type.BaseType;
        }
        return false;
    }

    private static Type GetGenericBaseType(Type type, Type genericBase)
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

