using ECK1.IntegrationContracts.Kafka.IntegrationRecords.Generated;
using Microsoft.Extensions.Configuration;

namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler;

/// <summary>
/// Extracts event-level fields from ThinEvent based on the integration manifest EventMappings section.
/// Used by ES, Mongo, and other plugins that need to enrich documents with event-level fields
/// (e.g. lastModified from ThinEvent.OccuredAt) that are not part of the FullRecord.
/// </summary>
public sealed class EventFieldExtractor
{
    public static readonly EventFieldExtractor Empty = new([]);

    private readonly (string FieldName, Func<ThinEvent, object> Getter)[] _fields;

    private EventFieldExtractor((string FieldName, Func<ThinEvent, object> Getter)[] fields)
        => _fields = fields;

    public bool HasFields => _fields.Length > 0;

    public IEnumerable<(string FieldName, object Value)> Extract(ThinEvent @event)
    {
        foreach (var (fieldName, getter) in _fields)
            yield return (fieldName, getter(@event));
    }

    public static EventFieldExtractor Compile(IConfigurationSection? eventMappings)
    {
        if (eventMappings is null || !eventMappings.GetChildren().Any())
            return Empty;

        var fields = new List<(string, Func<ThinEvent, object>)>();

        foreach (var mapping in eventMappings.GetChildren())
        {
            var fieldName = mapping.Key;
            var source = mapping["source"]
                ?? throw new InvalidOperationException($"EventMappings:{fieldName}:source is missing");

            fields.Add((fieldName, ResolveGetter(fieldName, source)));
        }

        return new EventFieldExtractor([.. fields]);
    }

    private static Func<ThinEvent, object> ResolveGetter(string fieldName, string source)
    {
        if (!source.StartsWith("event."))
            throw new InvalidOperationException(
                $"EventMappings:{fieldName} source must start with 'event.' but was '{source}'");

        var propertyName = source["event.".Length..];

        return propertyName switch
        {
            nameof(ThinEvent.EntityId) => e => e.EntityId,
            nameof(ThinEvent.EventId) => e => e.EventId,
            nameof(ThinEvent.EventType) => e => (object)e.EventType,
            nameof(ThinEvent.OccuredAt) => e => e.OccuredAt,
            nameof(ThinEvent.Version) => e => e.Version,
            _ => throw new InvalidOperationException(
                $"Unknown ThinEvent property '{propertyName}' in EventMappings:{fieldName}")
        };
    }
}
