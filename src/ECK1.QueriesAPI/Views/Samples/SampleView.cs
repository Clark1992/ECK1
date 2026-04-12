using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace ECK1.QueriesAPI.Views.Samples;

[BsonIgnoreExtraElements]
public class SampleView
{
    [JsonIgnore]
    public ObjectId Id { get; set; }

    public Guid SampleId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }

    [BsonElement("lastModified")]
    [BsonIgnoreIfNull]
    public DateTime? LastModified { get; set; }
    public SampleAddressView Address { get; set; }
    public List<SampleAttachmentView> Attachments { get; set; } = new();
}

[BsonNoId]
[BsonIgnoreExtraElements]
public class SampleAddressView
{
    [JsonIgnore]
    public Guid Id { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
}

[BsonNoId]
[BsonIgnoreExtraElements]
public class SampleAttachmentView
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string Url { get; set; }
}
