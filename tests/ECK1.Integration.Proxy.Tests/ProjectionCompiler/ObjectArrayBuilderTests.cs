using AutoFixture;
using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text.Json;
using Xunit.Abstractions;


namespace ECK1.Integration.Proxy.Tests.ProjectionCompiler;

public class ObjectArrayBuilderTests_SampleFullRecord2(ITestOutputHelper output)
{
    [Fact]
    public void ObjectArray_1Object()
    {
        var configData = new Dictionary<string, string>
        {
            ["mappings:format"] = "object[]",
            ["mappings:items:event_id:source"] = "event.EventId",
            ["mappings:items:event_id:type"] = "System.Guid",
            ["mappings:items:event_id:order"] = "0",
            ["mappings:items:event_type:source"] = "event.EventType",
            ["mappings:items:event_type:type"] = "string",
            ["mappings:items:event_type:order"] = "1",
            ["mappings:items:occurred_at:source"] = "event.OccuredAt",
            ["mappings:items:occurred_at:type"] = "System.DateTime",
            ["mappings:items:occurred_at:order"] = "2",
            ["mappings:items:entity_id:source"] = "record.SampleId",
            ["mappings:items:entity_id:type"] = "int",
            ["mappings:items:entity_id:order"] = "3",
            ["mappings:items:entity_type:source"] = "const.Sample",
            ["mappings:items:entity_type:type"] = "string",
            ["mappings:items:entity_type:order"] = "4",
            ["mappings:items:entity_version:source"] = "record.Version",
            ["mappings:items:entity_version:type"] = "int",
            ["mappings:items:entity_version:order"] = "5",


            ["mappings:items:payload:format"] = "json",
            ["mappings:items:payload:order"] = "6",
            ["mappings:items:payload:fields:sample_id"] = "record.SampleId",
            ["mappings:items:payload:fields:name"] = "record.Name",
            ["mappings:items:payload:fields:inner_name"] = "record.Inner.InnerName",
            ["mappings:items:payload:fields:movedToInner:name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:type"] = "array",
            ["mappings:items:payload:fields:attachments:context"] = "record.Attachments",
            ["mappings:items:payload:fields:attachments:items:filename"] = "item.FileName",
            ["mappings:items:payload:fields:attachments:items:url"] = "item.Url",
            ["mappings:items:payload:fields:attachments:items:name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:items:subObj:name"] = "item.FileName",

            ["mappings:items:payload:fields:attachments:items:subArr:type"] = "array",
            ["mappings:items:payload:fields:attachments:items:subArr:context"] = "item.SubAttachments",
            ["mappings:items:payload:fields:attachments:items:subArr:items:sub_name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:items:subArr:items:sub_att_filename"] = "item.SubFileName",

            ["mappings:items:payload:fields:attachments:items:subSubArr_L1:type"] = "array",
            ["mappings:items:payload:fields:attachments:items:subSubArr_L1:context"] = "item.SubSubAttachments",
            ["mappings:items:payload:fields:attachments:items:subSubArr_L1:items:sub_sub_L1_name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:items:subSubArr_L1:items:sub_sub_L1_att_filename"] = "item.SubSubFileName",

            ["mappings:items:payload:fields:attachments:items:subArr:items:subSubArr_L2:type"] = "array",
            ["mappings:items:payload:fields:attachments:items:subArr:items:subSubArr_L2:context"] = "item.SubSubAttachments",
            ["mappings:items:payload:fields:attachments:items:subArr:items:subSubArr_L2:items:sub_sub_L2_name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:items:subArr:items:subSubArr_L2:items:sub_sub_L2_att_filename"] = "item.SubSubFileName",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var mappingsSection = configuration.GetSection("mappings");

        var plan = ProjectionPlanCompiler.Compile<SampleThinEvent, TestSampleRecord>(mappingsSection);

        var thinEvent = new SampleThinEvent
        {
            EventId = Guid.NewGuid(),
            EntityId = Guid.NewGuid(),
            EventType = "test_event_type",
            OccuredAt = DateTime.UtcNow,
            Version = 432
        };

        var record = new TestSampleRecord
        {
            SampleId = 42,
            Version = 432,
            Name = "test-name",
            Inner = new InnerObject { InnerName = "test-inner-name"},
            Attachments = new[]
            {
                new Attachment
                {
                    FileName = "att_filename",
                    Url = "att_url",
                    SubAttachments = 
                    [
                        new SubAttachment
                        {
                            SubFileName = "sub_att_filename",
                            SubSubAttachments = 
                            [
                                new SubSubAttachment 
                                {
                                    SubSubFileName = "subsub2_filename",
                                }
                            ]
                        }
                    ],
                    SubSubAttachments =
                    [
                        new SubSubAttachment
                        {
                            SubSubFileName = "subsub1_filename",
                        }
                    ]
                }
            } 
        };

        var colValues = plan.ColumnValues(thinEvent, record);
        colValues.Should().BeEquivalentTo(new object[]
        {
            record.SampleId,
            "Sample",
            record.Version,
            thinEvent.EventId,
            thinEvent.EventType,
            thinEvent.OccuredAt,
            @"{
  ""attachments"": [
    {
      ""filename"": ""att_filename"",
      ""name"": ""test-name"",
      ""subArr"": [
        {
          ""subSubArr_L2"": [
            {
              ""sub_sub_L2_att_filename"": ""subsub2_filename"",
              ""sub_sub_L2_name"": ""test-name""
            }
          ],
          ""sub_att_filename"": ""sub_att_filename"",
          ""sub_name"": ""test-name""
        }
      ],
      ""subObj"": {
        ""name"": ""att_filename""
      },
      ""subSubArr_L1"": [
        {
          ""sub_sub_L1_att_filename"": ""subsub1_filename"",
          ""sub_sub_L1_name"": ""test-name""
        }
      ],
      ""url"": ""att_url""
    }
  ],
  ""inner_name"": ""test-inner-name"",
  ""movedToInner"": {
    ""name"": ""test-name""
  },
  ""name"": ""test-name"",
  ""sample_id"": 42
}"
        });
    }

#if DEBUG
    [Fact]
#else
    [Fact(Skip = "run manually")]
#endif
    public void ObjectArray_Benchmark()
    {
        var configData = new Dictionary<string, string>
        {
            ["mappings:format"] = "object[]",
            ["mappings:items:event_id:source"] = "event.EventId",
            ["mappings:items:event_id:type"] = "System.Guid",
            ["mappings:items:event_id:order"] = "0",
            ["mappings:items:event_type:source"] = "event.EventType",
            ["mappings:items:event_type:type"] = "string",
            ["mappings:items:event_type:order"] = "1",
            ["mappings:items:occurred_at:source"] = "event.OccuredAt",
            ["mappings:items:occurred_at:type"] = "System.DateTime",
            ["mappings:items:occurred_at:order"] = "2",
            ["mappings:items:entity_id:source"] = "record.SampleId",
            ["mappings:items:entity_id:type"] = "int",
            ["mappings:items:entity_id:order"] = "3",
            ["mappings:items:entity_type:source"] = "const.Sample",
            ["mappings:items:entity_type:type"] = "string",
            ["mappings:items:entity_type:order"] = "4",
            ["mappings:items:entity_version:source"] = "record.Version",
            ["mappings:items:entity_version:type"] = "int",
            ["mappings:items:entity_version:order"] = "5",


            ["mappings:items:payload:format"] = "json",
            ["mappings:items:payload:order"] = "6",
            ["mappings:items:payload:fields:sample_id"] = "record.SampleId",
            ["mappings:items:payload:fields:name"] = "record.Name",
            ["mappings:items:payload:fields:inner_name"] = "record.Inner.InnerName",
            ["mappings:items:payload:fields:movedToInner:name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:type"] = "array",
            ["mappings:items:payload:fields:attachments:context"] = "record.Attachments",
            ["mappings:items:payload:fields:attachments:items:filename"] = "item.FileName",
            ["mappings:items:payload:fields:attachments:items:url"] = "item.Url",
            ["mappings:items:payload:fields:attachments:items:name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:items:subObj:name"] = "item.FileName",

            ["mappings:items:payload:fields:attachments:items:subArr:type"] = "array",
            ["mappings:items:payload:fields:attachments:items:subArr:context"] = "item.SubAttachments",
            ["mappings:items:payload:fields:attachments:items:subArr:items:sub_name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:items:subArr:items:sub_att_filename"] = "item.SubFileName",

            ["mappings:items:payload:fields:attachments:items:subSubArr_L1:type"] = "array",
            ["mappings:items:payload:fields:attachments:items:subSubArr_L1:context"] = "item.SubSubAttachments",
            ["mappings:items:payload:fields:attachments:items:subSubArr_L1:items:sub_sub_L1_name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:items:subSubArr_L1:items:sub_sub_L1_att_filename"] = "item.SubSubFileName",

            ["mappings:items:payload:fields:attachments:items:subArr:items:subSubArr_L2:type"] = "array",
            ["mappings:items:payload:fields:attachments:items:subArr:items:subSubArr_L2:context"] = "item.SubSubAttachments",
            ["mappings:items:payload:fields:attachments:items:subArr:items:subSubArr_L2:items:sub_sub_L2_name"] = "record.Name",
            ["mappings:items:payload:fields:attachments:items:subArr:items:subSubArr_L2:items:sub_sub_L2_att_filename"] = "item.SubSubFileName",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var mappingsSection = configuration.GetSection("mappings");

        var plan = ProjectionPlanCompiler.Compile<SampleThinEvent, TestSampleRecord>(mappingsSection);


        var fixture = new Fixture();

        var count = 100000;
        var events = fixture.CreateMany<SampleThinEvent>(count).ToList();
        var records = fixture.CreateMany<TestSampleRecord>(count).ToList();

        var sw = new Stopwatch();

        sw.Start();

        for (var i = 0; i < records.Count; i++)
        {
            var @event = events[i];
            var record = records[i];

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = true
            });

            var colValues = plan.ColumnValues(@event, record);
        }

        sw.Stop();

        output.WriteLine($"{count} objects mapped in {sw.ElapsedMilliseconds} ms ({sw.ElapsedMilliseconds / (double)count} ms avg / {count / ((double)sw.ElapsedMilliseconds / 1000)} obj/s).");
    }
}


