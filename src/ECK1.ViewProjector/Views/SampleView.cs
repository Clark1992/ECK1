using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.Text.Json.Serialization;

namespace ECK1.ViewProjector.Views;

public class SampleView
{
    [JsonIgnore]
    public ObjectId Id { get; set; }

    public Guid SampleId { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
    public SampleAddressView Address { get; set; }
    public List<SampleAttachmentView> Attachments { get; set; } = new();
}

[BsonNoId]
public class SampleAddressView
{
    [JsonIgnore]
    public Guid Id { get; set; }
    public string Street { get; set; }
    public string City { get; set; }
    public string Country { get; set; }
}

[BsonNoId]
public class SampleAttachmentView
{
    public Guid Id { get; set; }
    public string FileName { get; set; }
    public string Url { get; set; }
}
