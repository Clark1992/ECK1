using ECK1.CommonUtils.Handler;
using System.Reflection;
using System.Runtime.CompilerServices;

using static ECK1.CommonUtils.Mapping.TypeUtils;

namespace ECK1.ReadProjector.Startup;

public static class GenericHandlerBootstrapper
{
    public static void Initialize(params Assembly[] assembliesToScan)
    {
        var handlerTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.BaseType != null && IsSubclassOfGeneric(t, typeof(GenericAsyncHandler<>)))
            .ToList();

        if (handlerTypes.Count == 0)
        {
            throw new Exception("Missing handlerTypes");
        }

        // foreach combination of generic types call static ctor
        foreach (var handlerType in handlerTypes)
        {
            var baseType = GetGenericBaseType(handlerType, typeof(GenericAsyncHandler<>));
            var argType = baseType.GetGenericArguments()[0];

            var baseHandlerType =  typeof(GenericAsyncHandler<>).MakeGenericType(argType);
            RuntimeHelpers.RunClassConstructor(baseHandlerType.TypeHandle);
        }
    }
}

