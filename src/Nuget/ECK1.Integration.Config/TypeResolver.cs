namespace ECK1.Integration.Config;

public static class TypeResolver
{
    public static Type? ResolveType(string fullTypeName) =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(fullTypeName))
            .FirstOrDefault(t => t is not null);
}
