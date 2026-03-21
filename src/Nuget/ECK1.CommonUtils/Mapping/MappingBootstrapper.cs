namespace ECK1.CommonUtils.Mapping;

public abstract class MappingByNameBootstrapper<TSrc, TDst>
{
    protected static readonly Dictionary<Type, Type> typeMapping
        = new();

    static MappingByNameBootstrapper()
    {
        typeMapping = TypeUtils.GetTypesMapping<TSrc, TDst>();
    }

    protected Type GetDestinationType(Type srcType)
    {
        if (typeMapping.TryGetValue(srcType, out var dstType))
        {
            return dstType;
        }
        else
        {
            throw new InvalidOperationException(
                $"No Type mapping found for {srcType.Name} -> {dstType.Name}");
        }
    }
}
