using AutoMapper;

using static ECK1.CommandsAPI.Utils.TypeUtils;

namespace ECK1.CommandsAPI.Mapping;

public class MapByInterface<TSourceInterface, TDestinationInterface> : Profile
{
    public MapByInterface()
    {
        var typeMapping = GetEventTypeMapping<TSourceInterface, TDestinationInterface>();

        foreach (var (sourceType, destinationType) in typeMapping)
        {
             CreateMap(sourceType, destinationType, MemberList.Destination);
        }
    }
}
