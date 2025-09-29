using System.Linq.Expressions;
using System.Reflection;

namespace ECK1.CommonUtils.Handler;


[AttributeUsage(AttributeTargets.Class)]
public class HandlerMethodAttribute : Attribute
{
    public string MethodName { get; }

    public HandlerMethodAttribute(string methodName)
    {
        MethodName = methodName;
    }
}

public static class HandlerRegistrar
{
    public static void RegisterHandlers<TValue, THandler, TDelegate>(
        Dictionary<Type, TDelegate> target,
        string defaultMethodName,
        Func<MethodInfo, bool> methodFilter,
        Func<MethodInfo, Type, TDelegate> compileDelegate)
    {
        var handlerTypes = Assembly.GetEntryAssembly()
            .GetTypes()
            .Where(t => typeof(THandler).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var type in handlerTypes)
        {
            var attr = type.GetCustomAttribute<HandlerMethodAttribute>();
            var methodName = attr?.MethodName ?? defaultMethodName;

            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                              .Where(m => m.Name == methodName && methodFilter(m));

            foreach (var method in methods)
            {
                var argType = method.GetParameters()[0].ParameterType;
                var del = compileDelegate(method, type);
                target[argType] = del;
            }
        }
    }
}

public abstract class GenericHandler<TValue> : GenericHandlerBase<TValue>
{
    private static readonly Dictionary<Type, Action<GenericHandler<TValue>, TValue>> _handlers = new();

    static GenericHandler()
    {
        HandlerRegistrar.RegisterHandlers<TValue, GenericHandler<TValue>, Action<GenericHandler<TValue>, TValue>>(
            _handlers,
            defaultMethodName: nameof(Handle),
            methodFilter: method =>
            {
                var parameters = method.GetParameters();
                return parameters.Length == 1 &&
                       typeof(TValue).IsAssignableFrom(parameters[0].ParameterType);
            },
            compileDelegate: (method, handlerType) =>
            {
                var handlerParam = Expression.Parameter(typeof(GenericHandler<TValue>), "handler");
                var argParam = Expression.Parameter(typeof(TValue), "arg");

                var call = Expression.Call(
                    Expression.Convert(handlerParam, handlerType),
                    method,
                    Expression.Convert(argParam, method.GetParameters()[0].ParameterType)
                );

                return Expression.Lambda<Action<GenericHandler<TValue>, TValue>>(call, handlerParam, argParam).Compile();
            });

    }

    protected void Handle(TValue arg)
    {
        var handler = GetHandler(_handlers, arg);

        handler(this, arg);
    }
}


public abstract class GenericAsyncHandler<TValue> : GenericHandlerBase<TValue>
{
    private static readonly Dictionary<Type, Func<GenericAsyncHandler<TValue>, TValue, CancellationToken, Task>> _asyncHandlers = new();

    static GenericAsyncHandler()
    {
        HandlerRegistrar.RegisterHandlers<TValue, GenericAsyncHandler<TValue>, Func<GenericAsyncHandler<TValue>, TValue, CancellationToken, Task>>(
            _asyncHandlers,
            defaultMethodName: nameof(Handle),
            methodFilter: method =>
            {
                var parameters = method.GetParameters();
                var returnType = method.ReturnType;
                return parameters.Length == 2 &&
                       typeof(TValue).IsAssignableFrom(parameters[0].ParameterType) &&
                       parameters[1].ParameterType == typeof(CancellationToken) &&
                       returnType == typeof(Task);
            },
            compileDelegate: (method, handlerType) =>
            {
                // Func<GenericAsyncHandler<T>, T, CancellationToken, Task>
                var handlerParam = Expression.Parameter(typeof(GenericAsyncHandler<TValue>), "handler");
                var argParam = Expression.Parameter(typeof(TValue), "arg");
                var tokenParam = Expression.Parameter(typeof(CancellationToken), "ct");

                var call = Expression.Call(
                    Expression.Convert(handlerParam, handlerType),
                    method,
                    Expression.Convert(argParam, method.GetParameters()[0].ParameterType),
                    tokenParam
                );

                return Expression.Lambda<Func<GenericAsyncHandler<TValue>, TValue, CancellationToken, Task>>(call, handlerParam, argParam, tokenParam).Compile();
            });
    }

    protected async Task Handle(TValue arg, CancellationToken ct)
    {
        var handler = GetHandler(_asyncHandlers, arg);

        await handler(this, arg, ct);
    }
}

public class GenericHandlerBase<TValue>
{
    protected THandler GetHandler<THandler>(Dictionary<Type, THandler> handlers, TValue arg)
    {
        var argType = arg.GetType();

        if (handlers.TryGetValue(argType, out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException($"No async handler for {argType.Name}");
    }
}
