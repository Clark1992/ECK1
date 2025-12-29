using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.Generated;
using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;

namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler;

public static class ProjectionPlanCompiler
{
    private readonly record struct CacheKey(
        Type EventType,
        Type RecordType,
        string MappingsPath,
        string Format,
        string ColumnsSignature);

    private static readonly ConcurrentDictionary<CacheKey, object> cache = new();

    public static ExecutionPlan<TEvent, TRecord> Compile<TEvent, TRecord>(
        IConfigurationSection mappings)
    {
        var format = mappings["format"];

        if (string.IsNullOrWhiteSpace(format))
            throw new InvalidOperationException("Mappings.format is missing");

        return format switch
        {
            "object[]" => CompileAsObjectArrayCached<TEvent, TRecord>(mappings),
            _ => throw new NotSupportedException("Unknown format.")
        };
    }

    private static ExecutionPlan<TEvent, TRecord> CompileAsObjectArrayCached<TEvent, TRecord>(
        IConfigurationSection mappings)
    {
        var itemsSection = mappings.GetSection("items");
        var items = itemsSection.GetChildren().ToArray();
        var columnNames = items.Select(item => item.Key).ToArray();

        var signature = string.Join("\u001f", columnNames);
        var key = new CacheKey(
            typeof(TEvent),
            typeof(TRecord),
            itemsSection.Path,
            "object[]",
            signature);

        var value = cache.GetOrAdd(key, _ => CompileAsObjectArray<TEvent, TRecord>(items, columnNames));
        return (ExecutionPlan<TEvent, TRecord>)value;
    }

    private static ExecutionPlan<TEvent, TRecord> CompileAsObjectArray<TEvent, TRecord>(
        IConfigurationSection[] items, string[] columnNames)
    {

        var accessors = new Func<TEvent, TRecord, object>[items.Length];
        for (var i = 0; i < items.Length; i++)
        {
            var accessor = CompileValue<TEvent, TRecord>(items[i]);
            if (accessor is null)
            {
                throw new InvalidOperationException("Cant find accessor for:");
            }
            accessors[i] = accessor;
        }

        Func<TEvent, TRecord, object[]> columnvalues = (evt, rec) =>
        {
            var values = new object[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                values[i] = accessors[i](evt, rec);
            }

            return values;
        };

        return new()
        {
            ColumnNames = columnNames,
            ColumnValues = columnvalues
        };
    }

    private static Func<TEvent, TRecord, object> CompileValue<TEvent, TRecord>(
        IConfigurationSection item)
    {
        var source = item["source"];
        var type = item["type"];
        var format = item["format"];

        if (source is not null)
        {
            return CompileScalar<TEvent, TRecord>(source, type);
        }
        else if (format is not null)
        {
            return CompileFormatted<TEvent, TRecord>(format, item.GetSection("fields"));
        }

        throw new InvalidOperationException("Wrong config in Mappings.Items");
    }

    private static Func<TEvent, TRecord, object> CompileScalar<TEvent, TRecord>(
        string source, string type)
    {
        try
        {
            if (source.StartsWith("const."))
            {
                var value = source["const.".Length..];
                var typedValue = type switch
                {
                    "string" => value,
                    "int" => Convert.ChangeType(value, typeof(int)),
                    _ => throw new NotSupportedException("Unknown type for const mapping.")
                };

                return (_, _) => typedValue;
            }

            if (source.StartsWith("event."))
            {
                switch (type)
                {
                    case "Guid":
                    case "System.Guid":
                        if (EventAccessor<TEvent>.TryGetGuid(
                            source["event.".Length..],
                            out var accessorGuid))
                        {
                            return (evt, _) =>
                            {
                                var value = accessorGuid(evt);
                                return value;
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException($"Can't find accessor for {source} [{type}] ");
                        }

                    case "int":
                        if (EventAccessor<TEvent>.TryGetInt(
                                    source["event.".Length..],
                                    out var accessorInt))
                        {
                            return (evt, _) =>
                            {
                                var value = accessorInt(evt);
                                return value;
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException($"Can't find accessor for {source} [{type}] ");
                        }

                    case "string":
                        if (EventAccessor<TEvent>.TryGetString(
                                    source["event.".Length..],
                                    out var accessorString))
                        {
                            return (evt, _) =>
                            {
                                var value = accessorString(evt);
                                return value;
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException($"Can't find accessor for {source} [{type}] ");
                        }

                    case "DateTime":
                    case "System.DateTime":
                        if (EventAccessor<TEvent>.TryGetDateTime(
                                    source["event.".Length..],
                                    out var accessorDateTime))
                        {
                            return (evt, _) =>
                            {
                                var value = accessorDateTime(evt);
                                return value;
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException($"Can't find accessor for {source} [{type}] ");
                        }
                }
            }

            if (source.StartsWith("record."))
            {
                switch (type)
                {
                    case "Guid":
                    case "System.Guid":
                        if (RecordAccessor<TRecord>.TryGetGuid(
                            source["record.".Length..],
                            out var accessorGuid))
                        {
                            return (_, rec) =>
                            {
                                var value = accessorGuid(rec);
                                return value;
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException($"Can't find accessor for {source} [{type}] ");
                        }

                    case "int":
                        if (RecordAccessor<TRecord>.TryGetInt(
                            source["record.".Length..],
                            out var accessorInt))
                        {
                            return (_, rec) =>
                            {
                                var value = accessorInt(rec);
                                return value;
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException($"Can't find accessor for {source} [{type}] ");
                        }

                    case "string":
                        if (RecordAccessor<TRecord>.TryGetString(
                            source["record.".Length..],
                            out var accessorString))
                        {
                            return (_, rec) =>
                            {
                                var value = accessorString(rec);
                                return value;
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException($"Can't find accessor for {source} [{type}] ");
                        }

                    case "DateTime":
                    case "System.DateTime":
                        if (RecordAccessor<TRecord>.TryGetDateTime(
                            source["record.".Length..],
                            out var accessorDateTime))
                        {
                            return (_, rec) =>
                            {
                                var value = accessorDateTime(rec);
                                return value;
                            };
                        }
                        else
                        {
                            throw new InvalidOperationException($"Can't find accessor for {source} [{type}] ");
                        }
                }
            }
        }
        catch (Exception e)
        {
            throw;
        }

        throw new InvalidOperationException("Invalid mapping");
    }

    private static Func<TEvent, TRecord, object> CompileFormatted<TEvent, TRecord>(
        string format,
        IConfigurationSection fieldsSection)
    {
        var rootBuilder = format switch
        {
            "json" => CompileJsonPayloadNode<TEvent, TRecord>(format, fieldsSection),
            _ => throw new InvalidOperationException("Unsupported payload format")
        };

        return rootBuilder;
    }

    private static Func<TEvent, TRecord, object> CompileJsonPayloadNode<TEvent, TRecord>(
        string format,
        IConfigurationSection fieldsSection)
    {
        var compiler = new JsonPlanCompiler<TEvent, TRecord>();
        var plan = compiler.Compile(format, fieldsSection);

        return (evt, record) =>
        {
            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions
            {
                Indented = true
            });

            plan.Execute(writer, evt, record);
            writer.Flush();

            return Encoding.UTF8.GetString(ms.ToArray());
        };
    }
}

