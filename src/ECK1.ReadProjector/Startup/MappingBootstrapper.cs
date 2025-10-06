using ECK1.CommonUtils.Mapping;
using ECK1.ReadProjector.Kafka.Orleans;
using System.Reflection;
using System.Runtime.CompilerServices;

using static ECK1.CommonUtils.Mapping.TypeUtils;

namespace ECK1.ReadProjector.Startup;

public static class MappingBootstrapper
{
    public static void Initialize(params Assembly[] assembliesToScan)
    {
        var type = typeof(OrleansKafkaAdapter<,,>);
        var handlerTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.BaseType != null &&
                IsSubclassOfGeneric(t, type))
            .ToList();

        if (handlerTypes.Count == 0)
        {
            throw new Exception("Missing IntegrationHandlerBootstrapper");
        }

        // foreach combination of generic types call static ctor
        foreach (var handlerType in handlerTypes)
        {
            var baseType = GetGenericBaseType(handlerType, type);
            var contractEventType = baseType.GetGenericArguments()[0];
            var viewEventType = baseType.GetGenericArguments()[1];

            var bootstrapperType = typeof(MappingByNameBootstrapper<,>).MakeGenericType(contractEventType, viewEventType);
            RuntimeHelpers.RunClassConstructor(bootstrapperType.TypeHandle);
        }
    }
}

