using AutoFixture;
using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json;
using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Sample;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Xunit.Abstractions;

namespace ECK1.Integration.Proxy.Tests.ProjectionCompiler.PayloadCompiler;

public class Utf8JsonBuilder3Tests_SampleFullRecord2(ITestOutputHelper output)
{
    [Fact]
    public void Json_1Object()
    {
        var configData = new Dictionary<string, string>
        {
            ["Payload:format"] = "json",
            ["Payload:fields:sample_id"] = "record.SampleId",
            ["Payload:fields:name"] = "record.Name",
            ["Payload:fields:inner_name"] = "record.Inner.InnerName",
            ["Payload:fields:movedToInner:name"] = "record.Name",
            ["Payload:fields:attachments:type"] = "array",
            ["Payload:fields:attachments:context"] = "record.Attachments",
            ["Payload:fields:attachments:items:filename"] = "item.FileName",
            ["Payload:fields:attachments:items:url"] = "item.Url",
            ["Payload:fields:attachments:items:name"] = "record.Name",
            ["Payload:fields:attachments:items:subObj:name"] = "item.FileName",

            ["Payload:fields:attachments:items:subArr:type"] = "array",
            ["Payload:fields:attachments:items:subArr:context"] = "item.SubAttachments",
            ["Payload:fields:attachments:items:subArr:items:sub_name"] = "record.Name",
            ["Payload:fields:attachments:items:subArr:items:sub_att_filename"] = "item.SubFileName",

            ["Payload:fields:attachments:items:subSubArr_L1:type"] = "array",
            ["Payload:fields:attachments:items:subSubArr_L1:context"] = "item.SubSubAttachments",
            ["Payload:fields:attachments:items:subSubArr_L1:items:sub_sub_L1_name"] = "record.Name",
            ["Payload:fields:attachments:items:subSubArr_L1:items:sub_sub_L1_att_filename"] = "item.SubSubFileName",

            ["Payload:fields:attachments:items:subArr:items:subSubArr_L2:type"] = "array",
            ["Payload:fields:attachments:items:subArr:items:subSubArr_L2:context"] = "item.SubSubAttachments",
            ["Payload:fields:attachments:items:subArr:items:subSubArr_L2:items:sub_sub_L2_name"] = "record.Name",
            ["Payload:fields:attachments:items:subArr:items:subSubArr_L2:items:sub_sub_L2_att_filename"] = "item.SubSubFileName",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var payloadSection = configuration.GetSection("Payload");

        var compiler = new JsonPlanCompiler<SampleThinEvent, TestSampleRecord>();
        var plan = compiler.Compile(payloadSection["format"], payloadSection.GetSection("fields"));

        var thinEvent = new SampleThinEvent
        {
            EventId = Guid.NewGuid(),
            EventType = "test_event_type",
            OccuredAt = DateTime.UtcNow,
            Version = 432
        };

        var record = new TestSampleRecord
        {
            SampleId = 42,
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

        using var ms = new MemoryStream();
        using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
        {
            Indented = true
        });

        plan.Execute(writer, thinEvent, record);
        writer.Flush();

        var json = Encoding.UTF8.GetString(ms.ToArray());

        json.Should().Be(@"{
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
}");
    }

#if DEBUG
    [Fact]
#else
    [Fact(Skip = "run manually")]
#endif
    public void Json_Benchmark()
    {
        var configData = new Dictionary<string, string>
        {
            ["Payload:format"] = "json",
            ["Payload:fields:sample_id"] = "record.SampleId",
            ["Payload:fields:name"] = "record.Name",
            ["Payload:fields:inner_name"] = "record.Inner.InnerName",
            ["Payload:fields:movedToInner:name"] = "record.Name",
            ["Payload:fields:attachments:type"] = "array",
            ["Payload:fields:attachments:context"] = "record.Attachments",
            ["Payload:fields:attachments:items:filename"] = "item.FileName",
            ["Payload:fields:attachments:items:url"] = "item.Url",
            ["Payload:fields:attachments:items:name"] = "record.Name",
            ["Payload:fields:attachments:items:subObj:name"] = "item.FileName",

            ["Payload:fields:attachments:items:subArr:type"] = "array",
            ["Payload:fields:attachments:items:subArr:context"] = "item.SubAttachments",
            ["Payload:fields:attachments:items:subArr:items:sub_name"] = "record.Name",
            ["Payload:fields:attachments:items:subArr:items:sub_att_filename"] = "item.SubFileName",

            ["Payload:fields:attachments:items:subSubArr_L1:type"] = "array",
            ["Payload:fields:attachments:items:subSubArr_L1:context"] = "item.SubSubAttachments",
            ["Payload:fields:attachments:items:subSubArr_L1:items:sub_sub_L1_name"] = "record.Name",
            ["Payload:fields:attachments:items:subSubArr_L1:items:sub_sub_L1_att_filename"] = "item.SubSubFileName",

            ["Payload:fields:attachments:items:subArr:items:subSubArr_L2:type"] = "array",
            ["Payload:fields:attachments:items:subArr:items:subSubArr_L2:context"] = "item.SubSubAttachments",
            ["Payload:fields:attachments:items:subArr:items:subSubArr_L2:items:sub_sub_L2_name"] = "record.Name",
            ["Payload:fields:attachments:items:subArr:items:subSubArr_L2:items:sub_sub_L2_att_filename"] = "item.SubSubFileName",
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

        var payloadSection = configuration.GetSection("Payload");

        var compiler = new JsonPlanCompiler<SampleThinEvent, TestSampleRecord>();
        var plan = compiler.Compile(payloadSection["format"], payloadSection.GetSection("fields"));

        var thinEvent = new SampleThinEvent
        {
        };

        var fixture = new Fixture();

        var count = 100000;
        var records = fixture.CreateMany<TestSampleRecord>(count).ToList();


        var sw = new Stopwatch();

        sw.Start();

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = true
            });

            plan.Execute(writer, thinEvent, record);
            writer.Flush();

            var json = Encoding.UTF8.GetString(ms.ToArray());
        }

        sw.Stop();

        output.WriteLine($"{count} objects mapped in {sw.ElapsedMilliseconds} ms ({sw.ElapsedMilliseconds / (double) count} ms avg / {count / ((double) sw.ElapsedMilliseconds / 1000)} obj/s).");
    }
}


