using Confluent.Kafka;
using Confluent.SchemaRegistry;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using ProtoBuf.Meta;

namespace ECK1.Kafka.ProtoBuf;

/// <summary>
/// Protobuf-net serializer for Kafka that integrates with Confluent Schema Registry.
/// It fetches the latest schema for the subject and compares generated schema for T using <see cref="ProtoSchemaComparer"/>.
/// </summary>
public class ProtobufNetSerializer<T> : IAsyncSerializer<T>
{
    private readonly ISchemaRegistryClient _schemaRegistry;
    private readonly ProtoSerdeConfig _config;
    private readonly ILogger<ProtobufNetSerializer<T>> _logger;
    private string _subject;
    private int? _schemaId;
    private string _registrySchemaText;

    public ProtobufNetSerializer(
        ISchemaRegistryClient schemaRegistry,
        ILogger<ProtobufNetSerializer<T>> logger,
        ProtoSerdeConfig config = null)
    {
        _schemaRegistry = schemaRegistry ?? throw new ArgumentNullException(nameof(schemaRegistry));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? new();
    }

    private async Task EnsureLatestSchemaAsync(Confluent.Kafka.SerializationContext context)
    {
        _subject = _subject ?? GetSubject(context);

        if (_schemaId.HasValue && !string.IsNullOrEmpty(_registrySchemaText))
            return;

        try
        {
            var latest = await _schemaRegistry.GetLatestSchemaAsync(_subject) 
                ?? throw new InvalidOperationException($"No latest schema found for subject '{_subject}'");

            _schemaId = latest.Id;
            
            // RegisteredSchema typically exposes SchemaString. Use SchemaString if available, otherwise try Schema.
            _registrySchemaText = latest.SchemaString;

            if (string.IsNullOrEmpty(_registrySchemaText))
                throw new InvalidOperationException($"Failed to obtain schema text for subject '{_subject}' from registry.");

            _logger.LogInformation("Fetched schema id {SchemaId} for subject {Subject}", _schemaId, _subject);

            ValidateMessageType();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch latest schema for subject {Subject}", _subject);
            throw;
        }
    }

    private string GetSubject(Confluent.Kafka.SerializationContext context)
    {
        var suffix = context.Component == MessageComponentType.Key ? "key" : "value";
        var subjectMain = _config.SubjectNameStrategy switch
        {
            SubjectNameStrategy.Record => typeof(T).Name.ToLowerInvariant(),
            SubjectNameStrategy.Topic => context.Topic,
            SubjectNameStrategy.TopicRecord => $"{context.Topic}-{typeof(T).Name.ToLowerInvariant()}",
            _ => throw new InvalidOperationException($"Unexpected SubjectNameStrategy")
        };

        return $"{subjectMain}-{suffix}";
    }

    public async Task<byte[]> SerializeAsync(T data, Confluent.Kafka.SerializationContext context)
    {
        if (data == null)
            return null;

        await EnsureLatestSchemaAsync(context);

        using var ms = new MemoryStream();

        // Schema Registry header: [magic byte = 0][schema id (int32 big endian)]
        ms.WriteByte(0);
        if (!_schemaId.HasValue)
            throw new InvalidOperationException("Schema id is not available.");

        var schemaIdBytes = BitConverter.GetBytes(_schemaId.Value);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(schemaIdBytes);
        ms.Write(schemaIdBytes, 0, schemaIdBytes.Length);

        // Serialize payload via protobuf-net
        Serializer.Serialize(ms, data);

        return ms.ToArray();
    }

    private void ValidateMessageType()
    {
        var type = typeof(T);

        var options = new SchemaGenerationOptions
        {
            Syntax = ProtoSyntax.Proto3,
            Package = $"{type.Namespace}.Tmp",
        };
        options.Types.Add(type);

        var schemaOfT = RuntimeTypeModel.Default.GetSchema(options);

        var (isValid, error) = ProtoSchemaComparer.Compare(schemaOfT, _registrySchemaText);
        if (!isValid)
        {
            _logger.LogError("Generated schema for {Type} does not match registry schema for subject {Subject}. Message: {error}", type.FullName, _subject, error);
            throw new InvalidOperationException($"Generated schema does not match registry schema. Message: {error}");
        }
    }
}
