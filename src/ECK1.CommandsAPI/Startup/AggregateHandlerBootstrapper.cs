using ECK1.CommandsAPI.Domain;
using ECK1.CommonUtils.Handler;
using System.Reflection;
using System.Runtime.CompilerServices;

using static ECK1.CommonUtils.Mapping.TypeUtils;

namespace ECK1.CommandsAPI.Startup;

public static class AggregateHandlerBootstrapper
{
    public static void Initialize(params Assembly[] assembliesToScan)
    {
        var aggregateTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.BaseType != null && IsSubclassOfGeneric(t, typeof(AggregateRoot<>)))
            .ToList();

        if (aggregateTypes.Count == 0)
        {
            throw new Exception("Missing AggregateHandlerBootstrapper");
        }

        // foreach combination of generic types call static ctor
        foreach (var aggregateType in aggregateTypes)
        {
            var baseType = GetGenericBaseType(aggregateType, typeof(AggregateRoot<>));
            var eventType = baseType.GetGenericArguments()[0];

            var factoryType = typeof(AggregateFactory<,>).MakeGenericType(aggregateType, eventType);
            RuntimeHelpers.RunClassConstructor(factoryType.TypeHandle);

            var baseHandlerType =  typeof(GenericHandler<>).MakeGenericType(eventType);
            RuntimeHelpers.RunClassConstructor(baseHandlerType.TypeHandle);
        }
    }
}

