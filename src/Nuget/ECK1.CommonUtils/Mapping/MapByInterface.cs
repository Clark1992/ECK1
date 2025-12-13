using AutoMapper;

using static ECK1.CommonUtils.Mapping.TypeUtils;

namespace ECK1.CommonUtils.Mapping;

public class MapByInterface<TSourceInterface, TDestinationInterface> : Profile
{
    public MapByInterface()
    {
        var typeMapping = GetTypesMappingWithNested<TSourceInterface, TDestinationInterface>();

        foreach (var (sourceType, destinationType) in typeMapping)
        {
            CreateMap(sourceType, destinationType);
        }
    }
}
