using AutoMapper;
using ECK1.CommandsAPI.Mapping;

namespace ECK1.CommandsAPI.Tests;

public class ProfilesTest
{
    [Fact]
    public void Configuration_IsValid()
    {
        var coreAssembly = typeof(MapByInterface<,>).Assembly;

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(coreAssembly);
        });

        config.AssertConfigurationIsValid();
    }
}