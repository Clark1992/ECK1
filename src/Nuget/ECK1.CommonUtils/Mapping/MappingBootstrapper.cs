namespace ECK1.CommonUtils.Mapping;

public abstract class MappingByNameBootstrapper<TSrc, TDst>
{
    protected static readonly Dictionary<Type, Type> eventMapping
        = new();

    static MappingByNameBootstrapper()
    {
        eventMapping = TypeUtils.GetTypesMapping<TSrc, TDst>();
    }

    protected Type GetDestinationType(Type srcType)
    {
        if (eventMapping.TryGetValue(srcType, out var dstType))
        {
            return dstType;
        }
        else
        {
            throw new InvalidOperationException(
                $"No Event mapping found for {srcType.Name}");
        }
    }
}
