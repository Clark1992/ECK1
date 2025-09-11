using System.Text.Json;
using Confluent.Kafka;
using MediatR;
using Microsoft.Extensions.Options;
using ECK1.ReadProjector.Notifications;
using ECK1.BusinessEvents.Sample;

namespace ECK1.ReadProjector.Kafka;

public class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string GroupId { get; set; } = "read-projector-group";
    public string Topic { get; set; } = "sample-business-events";
}

public sealed class ReadProjectorConsumer : BackgroundService
{
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly IMediator _mediator;
    private readonly KafkaOptions _options;
    private readonly JsonSerializerOptions _jsonOptions;

    public ReadProjectorConsumer(IOptions<KafkaOptions> options, IMediator mediator)
    {
        _options = options.Value;
        _mediator = mediator;

        var config = new ConsumerConfig
        {
            BootstrapServers = _options.BootstrapServers,
            GroupId = _options.GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<Ignore, string>(config).Build();

        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        };
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _consumer.Subscribe(_options.Topic);

        return Task.Run(() =>
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var cr = _consumer.Consume(stoppingToken);
                        if (cr?.Message == null) continue;

                        var payload = cr.Message.Value;

                        // Try to deserialize polymorphic ISampleEvent
                        ISampleEvent ev = null;
                        try
                        {
                            ev = JsonSerializer.Deserialize<ISampleEvent>(payload, _jsonOptions);
                        }
                        catch (Exception ex)
                        {
                            // If polymorphic fails, attempt to read as JsonDocument and inspect $type
                            using var doc = JsonDocument.Parse(payload);
                            if (doc.RootElement.TryGetProperty("$type", out var t))
                            {
                                var typeName = t.GetString();
                                // Attempt mapping by name
                                ev = typeName switch
                                {
                                    nameof(SampleCreatedEvent) => JsonSerializer.Deserialize<SampleCreatedEvent>(payload, _jsonOptions),
                                    nameof(SampleNameChangedEvent) => JsonSerializer.Deserialize<SampleNameChangedEvent>(payload, _jsonOptions),
                                    nameof(SampleDescriptionChangedEvent) => JsonSerializer.Deserialize<SampleDescriptionChangedEvent>(payload, _jsonOptions),
                                    nameof(SampleAddressChangedEvent) => JsonSerializer.Deserialize<SampleAddressChangedEvent>(payload, _jsonOptions),
                                    nameof(SampleAttachmentAddedEvent) => JsonSerializer.Deserialize<SampleAttachmentAddedEvent>(payload, _jsonOptions),
                                    nameof(SampleAttachmentRemovedEvent) => JsonSerializer.Deserialize<SampleAttachmentRemovedEvent>(payload, _jsonOptions),
                                    nameof(SampleAttachmentUpdatedEvent) => JsonSerializer.Deserialize<SampleAttachmentUpdatedEvent>(payload, _jsonOptions),
                                    _ => null
                                };
                            }
                        }

                        if (ev != null)
                        {
                            // publish MediatR notification
                            _mediator.Send(new EventNotification<ISampleEvent>(ev)).GetAwaiter().GetResult();
                        }

                        _consumer.Commit(cr);
                    }
                    catch (ConsumeException cex)
                    {
                        // log consume-specific errors
                        Console.WriteLine($"Kafka consume error: {cex.Error.Reason}");
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Kafka processing error: {ex}");
                    }
                }
            }
            finally
            {
                _consumer.Close();
            }
        }, stoppingToken);
    }

    public override void Dispose()
    {
        _consumer.Dispose();
        base.Dispose();
    }
}
