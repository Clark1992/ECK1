using AutoFixture;
using AutoMapper;
using ECK1.ViewProjector.Mapping;

namespace ECK1.ViewProjector.Tests;

public class ProfilesTest
{
    [Fact]
    public void Configuration_IsValid()
    {
        var coreAssembly = typeof(SampleMapping).Assembly;

        var config = new MapperConfiguration(cfg =>
        {
            cfg.AddMaps(coreAssembly);
        });

        config.AssertConfigurationIsValid();
    }
}
