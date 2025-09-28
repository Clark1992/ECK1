using ECK1.CommandsAPI.Commands;
using ECK1.CommandsAPI.Domain;
using System.Reflection;
using System.Runtime.CompilerServices;

using static ECK1.CommonUtils.Mapping.TypeUtils;

namespace ECK1.CommandsAPI.Startup;

public static class IntegrationHandlerBootstrapper
{
    public static void Initialize(params Assembly[] assembliesToScan)
    {
        var handlerTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.BaseType != null && IsSubclassOfGeneric(t, typeof(IntegrationBase<,>)))
            .ToList();

        if (handlerTypes.Count == 0)
        {
            throw new Exception("Missing IntegrationHandlerBootstrapper");
        }

        // foreach combination of generic types call static ctor
        foreach (var handlerType in handlerTypes)
        {
            var baseType = GetGenericBaseType(handlerType, typeof(IntegrationBase<,>));
            var domainEventType = baseType.GetGenericArguments()[0];
            var contractEventType = baseType.GetGenericArguments()[1];

            var bootstrapperType = typeof(IntegrationBootstrapper<,>).MakeGenericType(domainEventType, contractEventType);
            RuntimeHelpers.RunClassConstructor(bootstrapperType.TypeHandle);
        }
    }
}

