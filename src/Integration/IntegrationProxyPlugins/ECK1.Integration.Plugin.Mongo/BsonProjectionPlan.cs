using MongoDB.Bson;
using System.Linq.Expressions;
using System.Reflection;

namespace ECK1.Integration.Plugin.Mongo;

/// <summary>
/// A compiled, cached projection plan that builds a BsonDocument from TMessage
/// containing only the fields specified in the manifest Fields config.
/// Built once at startup per TMessage type; reused on every push.
///
/// Similar to JsonPlanCompiler: analyzes config once → compiles plan → executes per message.
/// Uses reflection + compiled expression delegates for property access.
/// </summary>
internal sealed class BsonProjectionPlan<TMessage>
{
    private readonly Action<BsonDocument, TMessage>[] _writers;

    private BsonProjectionPlan(Action<BsonDocument, TMessage>[] writers)
        => _writers = writers;

    public BsonDocument Project(TMessage message)
    {
        var doc = new BsonDocument();
        foreach (var write in _writers)
            write(doc, message);
        return doc;
    }

    public static BsonProjectionPlan<TMessage> Compile(List<string> fieldPaths)
    {
        var root = FieldNode.Parse(fieldPaths);
        Action<BsonDocument, TMessage>[] writers = PlanCompiler.CompileWriters<TMessage>(root);
        return new BsonProjectionPlan<TMessage>(writers);
    }
}

file sealed class FieldNode
{
    public Dictionary<string, FieldNode> Children { get; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsLeaf => Children.Count == 0;

    public static FieldNode Parse(IEnumerable<string> paths)
    {
        var root = new FieldNode();
        foreach (var path in paths)
        {
            var segments = path.Split('.');
            FieldNode current = root;
            foreach (var segment in segments)
            {
                if (!current.Children.TryGetValue(segment, out FieldNode child))
                {
                    child = new FieldNode();
                    current.Children[segment] = child;
                }
                current = child;
            }
        }
        return root;
    }
}

file static class PlanCompiler
{
    private const BindingFlags PropFlags =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;

    public static Action<BsonDocument, T>[] CompileWriters<T>(FieldNode node)
    {
        var writers = new List<Action<BsonDocument, T>>();

        foreach (var (name, child) in node.Children)
        {
            PropertyInfo prop = typeof(T).GetProperty(name, PropFlags)
                ?? throw new InvalidOperationException(
                    $"Property '{name}' not found on type {typeof(T).Name}");

            bool isCollection = TryGetItemType(prop.PropertyType, out Type itemType);

            if (child.IsLeaf && !isCollection)
            {
                writers.Add(CompileScalar<T>(name, prop));
            }
            else if (child.IsLeaf && isCollection)
            {
                writers.Add(InvokeGeneric<Action<BsonDocument, T>>(
                    nameof(CompileScalarArray), [typeof(T), itemType!], [name, prop]));
            }
            else if (isCollection)
            {
                writers.Add(InvokeGeneric<Action<BsonDocument, T>>(
                    nameof(CompileObjectArray), [typeof(T), itemType!], [name, prop, child]));
            }
            else
            {
                writers.Add(InvokeGeneric<Action<BsonDocument, T>>(
                    nameof(CompileObject), [typeof(T), prop.PropertyType], [name, prop, child]));
            }
        }

        return [.. writers];
    }

    private static Action<BsonDocument, TSource> CompileScalar<TSource>(string name, PropertyInfo prop)
    {
        Func<TSource, object> getter = CompileGetter<TSource>(prop);
        Func<object, BsonValue> convert = GetBsonConverter(prop.PropertyType);
        return (doc, src) => doc.Add(name, convert(getter(src)));
    }

    private static Action<BsonDocument, TSource> CompileScalarArray<TSource, TItem>(
        string name, PropertyInfo prop)
    {
        Func<TSource, IEnumerable<TItem>> getter = CompileEnumerableGetter<TSource, TItem>(prop);
        Func<object, BsonValue> convert = GetBsonConverter(typeof(TItem));
        return (doc, src) =>
        {
            IEnumerable<TItem> items = getter(src);
            if (items is null) { doc.Add(name, BsonNull.Value); return; }
            var array = new BsonArray();
            foreach (TItem item in items)
                array.Add(convert(item!));
            doc.Add(name, array);
        };
    }

    private static Action<BsonDocument, TSource> CompileObjectArray<TSource, TItem>(
        string name, PropertyInfo prop, FieldNode childNode)
    {
        Func<TSource, IEnumerable<TItem>> getter = CompileEnumerableGetter<TSource, TItem>(prop);
        Action<BsonDocument, TItem>[] itemWriters = CompileWriters<TItem>(childNode);
        return (doc, src) =>
        {
            IEnumerable<TItem> items = getter(src);
            if (items is null) { doc.Add(name, BsonNull.Value); return; }
            var array = new BsonArray();
            foreach (TItem item in items)
            {
                var itemDoc = new BsonDocument();
                foreach (Action<BsonDocument, TItem> w in itemWriters)
                    w(itemDoc, item);
                array.Add(itemDoc);
            }
            doc.Add(name, array);
        };
    }

    private static Action<BsonDocument, TSource> CompileObject<TSource, TNested>(
        string name, PropertyInfo prop, FieldNode childNode)
    {
        Func<TSource, TNested> getter = CompileTypedGetter<TSource, TNested>(prop);
        Action<BsonDocument, TNested>[] innerWriters = CompileWriters<TNested>(childNode);
        return (doc, src) =>
        {
            TNested nested = getter(src);
            if (nested is null) { doc.Add(name, BsonNull.Value); return; }
            var innerDoc = new BsonDocument();
            foreach (Action<BsonDocument, TNested> w in innerWriters)
                w(innerDoc, nested);
            doc.Add(name, innerDoc);
        };
    }

    // --- Expression compilation ---

    private static Func<T, object> CompileGetter<T>(PropertyInfo prop)
    {
        ParameterExpression param = Expression.Parameter(typeof(T));
        MemberExpression access = Expression.Property(param, prop);
        UnaryExpression boxed = Expression.Convert(access, typeof(object));
        return Expression.Lambda<Func<T, object>>(boxed, param).Compile();
    }

    private static Func<TSource, TNested> CompileTypedGetter<TSource, TNested>(PropertyInfo prop)
    {
        ParameterExpression param = Expression.Parameter(typeof(TSource));
        MemberExpression access = Expression.Property(param, prop);
        return Expression.Lambda<Func<TSource, TNested>>(access, param).Compile();
    }

    private static Func<TSource, IEnumerable<TItem>> CompileEnumerableGetter<TSource, TItem>(PropertyInfo prop)
    {
        ParameterExpression param = Expression.Parameter(typeof(TSource));
        MemberExpression access = Expression.Property(param, prop);
        Expression body = typeof(IEnumerable<TItem>).IsAssignableFrom(prop.PropertyType)
            ? access
            : Expression.Convert(access, typeof(IEnumerable<TItem>));
        return Expression.Lambda<Func<TSource, IEnumerable<TItem>>>(body, param).Compile();
    }

    // --- BsonValue conversion ---

    private static Func<object, BsonValue> GetBsonConverter(Type type)
    {
        Type t = Nullable.GetUnderlyingType(type) ?? type;

        if (t == typeof(string))   return v => v is string s ? (BsonValue)new BsonString(s) : BsonNull.Value;
        if (t == typeof(Guid))     return v => v is Guid g ? new BsonBinaryData(g, GuidRepresentation.Standard) : BsonNull.Value;
        if (t == typeof(int))      return v => v is int i ? (BsonValue)new BsonInt32(i) : BsonNull.Value;
        if (t == typeof(long))     return v => v is long l ? (BsonValue)new BsonInt64(l) : BsonNull.Value;
        if (t == typeof(double))   return v => v is double d ? (BsonValue)new BsonDouble(d) : BsonNull.Value;
        if (t == typeof(float))    return v => v is float f ? (BsonValue)new BsonDouble(f) : BsonNull.Value;
        if (t == typeof(decimal))  return v => v is decimal m ? (BsonValue)BsonDecimal128.Create(m) : BsonNull.Value;
        if (t == typeof(DateTime)) return v => v is DateTime dt ? (BsonValue)new BsonDateTime(dt) : BsonNull.Value;
        if (t == typeof(bool))     return v => v is bool b ? (BsonValue)(b ? BsonBoolean.True : BsonBoolean.False) : BsonNull.Value;
        if (t.IsEnum)              return v => v is not null ? (BsonValue)new BsonInt32((int)v) : BsonNull.Value;

        throw new InvalidOperationException(
            $"Unsupported scalar type '{type.Name}' for BsonProjectionPlan. " +
            $"Add a converter or restructure the field as a nested object.");
    }

    // --- Helpers ---

    private static bool TryGetItemType(Type type, out Type itemType)
    {
        if (type != typeof(string))
        {
            foreach (Type iface in type.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                {
                    itemType = iface.GetGenericArguments()[0];
                    return true;
                }
            }
        }
        itemType = null;
        return false;
    }

    private static TResult InvokeGeneric<TResult>(
        string methodName, Type[] typeArgs, object[] args)
    {
        MethodInfo method = typeof(PlanCompiler)
            .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeArgs);
        return (TResult)method.Invoke(null, args)!;
    }
}
