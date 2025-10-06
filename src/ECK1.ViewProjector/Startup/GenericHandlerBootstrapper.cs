using ECK1.CommonUtils.Handler;
using System.Reflection;
using System.Runtime.CompilerServices;

using static ECK1.CommonUtils.Mapping.TypeUtils;

namespace ECK1.ViewProjector.Startup;

public static class GenericHandlerBootstrapper
{
    public static void Initialize(params Assembly[] assembliesToScan)
    {
        List<Type> types = [typeof(GenericAsyncHandler<>), typeof(GenericAsyncHandler<,>)];

        var totalCount = 0;
        foreach (Type type in types)
        {
            var handlerTypes = assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => !t.IsAbstract && t.BaseType != null &&
                IsSubclassOfGeneric(t, type))
            .ToList();

            totalCount += handlerTypes.Count;

            // foreach combination of generic types call static ctor
            foreach (var handlerType in handlerTypes)
            {
                var baseType = GetGenericBaseType(handlerType, type);
                var genericArgs = baseType.GetGenericArguments();

                //var argType = baseType.GetGenericArguments()[0];
                Type baseHandlerType = type.MakeGenericType(genericArgs.ToArray());

                RuntimeHelpers.RunClassConstructor(baseHandlerType.TypeHandle);
            }
        }

        if (totalCount == 0)
        {
            throw new Exception("Missing handlerTypes");
        }
    }
}

