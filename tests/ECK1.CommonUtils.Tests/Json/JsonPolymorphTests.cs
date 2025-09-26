using AutoFixture;
using AutoFixture.Xunit2;
using ECK1.Contracts.Kafka.BusinessEvents.Sample;
using Newtonsoft.Json;

namespace ECK1.CommonUtils.Tests.Json;

public class JsonPolymorphTests
{

    [Theory]
    [AutoData]
    public void Polymorph_SerializesCorrectly(SampleCreatedEvent obj)
    {
        //Arrange
        var objAbstract = obj as ISampleEvent;
        var expected = System.Text.Json.JsonSerializer.Serialize(objAbstract);

        // Act
        var actual = JsonConvert.SerializeObject(objAbstract);


        // Assert
        actual.Should().Be(expected);
    }

    [Theory]
    [AutoData]
    public void Polymorph_DeserializesCorrectly(SampleCreatedEvent obj)
    {
        //Arrange
        var expected = obj as ISampleEvent;
        var serialized = System.Text.Json.JsonSerializer.Serialize(expected);

        // Act
        var actual = JsonConvert.DeserializeObject<ISampleEvent>(serialized);

        // Assert
        actual.Should().BeOfType<SampleCreatedEvent>();
        (actual as SampleCreatedEvent).Should().BeEquivalentTo(expected);
    }

    [Theory(Skip = "run manually")]
    [AutoData]
    public void Polymorph_DeserializesCorrectly2(SampleCreatedEvent obj)
    {
        //Arrange
        var expected = obj as ISampleEvent;
        var serialized = System.Text.Json.JsonSerializer.Serialize(expected);

        var system = System.Text.Json.JsonSerializer.Deserialize<ISampleEvent>(serialized);
        var ns = JsonConvert.DeserializeObject<ISampleEvent>(serialized);

        var fixture = new Fixture();
        const int cnt = 100_000;
        var objects = fixture.CreateMany<SampleCreatedEvent>(cnt).Cast<ISampleEvent>().ToList();
        var serializedObjects = objects.Select(x => System.Text.Json.JsonSerializer.Serialize(x)).ToList();

        var sw = new System.Diagnostics.Stopwatch();


        sw.Start();
        foreach (var s in serializedObjects)
        {
            var des = System.Text.Json.JsonSerializer.Deserialize<ISampleEvent>(s);
        }

        sw.Stop();

        Console.WriteLine($"System.Text elapsed: {sw.ElapsedMilliseconds}, AvgTicks = {sw.ElapsedTicks / cnt}");

        sw.Reset();

        sw.Start();
        foreach (var s in serializedObjects)
        {
            var des = JsonConvert.DeserializeObject<ISampleEvent>(s);
        }
        sw.Stop();
        
        Console.WriteLine($"Newtonsoft elapsed: {sw.ElapsedMilliseconds}, AvgTicks = {sw.ElapsedTicks / cnt}");

        // Act
        var actual = JsonConvert.DeserializeObject<ISampleEvent>(serialized);

        // Assert
        actual.Should().BeOfType<SampleCreatedEvent>();
    }

}
