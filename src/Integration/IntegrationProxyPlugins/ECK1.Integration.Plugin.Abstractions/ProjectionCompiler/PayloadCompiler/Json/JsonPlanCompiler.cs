using ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.Generated;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace ECK1.Integration.Plugin.Abstractions.ProjectionCompiler.PayloadCompiler.Json;

public sealed class JsonPlanCompiler<TEvent, TRecord> : IJsonPlanCompiler<TEvent, TRecord>
{
    public JsonExecutionPlan<TEvent, TRecord> Compile(
        string format,
        IConfigurationSection fieldsSection)
    {
        ValidatePayload(format);

        if (!fieldsSection.Exists())
            throw new InvalidOperationException("Payload.fields is required");

        var ops = new List<JsonOp<TEvent, TRecord>>
        {
            JsonOp<TEvent, TRecord>.ObjStart()
        };

        foreach (var field in fieldsSection.GetChildren())
        {
            if (field["type"] == "array")
            {
                this.ArrayCompile(field, ops);
                continue;
            }

            CompileField(field, ops);
        }

        ops.Add(JsonOp<TEvent, TRecord>.ObjEnd());

        return new JsonExecutionPlan<TEvent, TRecord>(ops.ToArray());
    }

    private static void ValidatePayload(string format)
    {
        if (!string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Payload format '{format}' is not supported");
    }

    private static void CompileField(
        IConfigurationSection field,
        List<JsonOp<TEvent, TRecord>> ops)
    {
        ops.Add(JsonOp<TEvent, TRecord>.NameOf(field.Key));

        if (field.Value != null)
        {
            // scalar
            var emitter = CompileEmitter(field.Value);
            ops.Add(JsonOp<TEvent, TRecord>.Emit(emitter));
            return;
        }

        // object
        ops.Add(JsonOp<TEvent, TRecord>.ObjStart());

        foreach (var child in field.GetChildren())
        {
            CompileField(child, ops);
        }

        ops.Add(JsonOp<TEvent, TRecord>.ObjEnd());
    }

    private static Action<Utf8JsonWriter, JsonExecutionContext<TEvent, TRecord>> CompileEmitter(string mapping)
    {
        if (mapping.StartsWith("record."))
        {
            return CompileRecordEmitter(mapping);
        }

        throw new InvalidOperationException(
           $"Wrong mapping:{mapping}");
    }

    private static Action<Utf8JsonWriter, JsonExecutionContext<TEvent, TRecord>> CompileRecordEmitter(string mapping)
    {
        if (!mapping.StartsWith("record.", StringComparison.Ordinal))
            throw new NotSupportedException($"Unsupported mapping '{mapping}'");

        var propertyName = mapping["record.".Length..];

        if (RecordAccessor<TRecord>.TryGetInt(propertyName, out var intGetter))
        {
            return (w, ctx) =>
                w.WriteNumberValue(intGetter(ctx.Record));
        }

        if (RecordAccessor<TRecord>.TryGetString(propertyName, out var stringGetter))
        {
            return (w, ctx) =>
                w.WriteStringValue(stringGetter(ctx.Record));
        }

        if (RecordAccessor<TRecord>.TryGetGuid(propertyName, out var guidGetter))
        {
            return (w, ctx) =>
                w.WriteStringValue(guidGetter(ctx.Record));
        }

        if (RecordAccessor<TRecord>.TryGetDecimal(propertyName, out var decimalGetter))
        {
            return (w, ctx) =>
                w.WriteNumberValue(decimalGetter(ctx.Record));
        }

        throw new InvalidOperationException(
            $"Field '{propertyName}' not found on {typeof(TRecord).Name}");
    }

    private static Action<Utf8JsonWriter, JsonExecutionContext<TEvent, TRecord>> CompileItemEmitter<TItem>(string mapping)
    {
        if (!mapping.StartsWith("item.", StringComparison.Ordinal))
            throw new NotSupportedException($"Unsupported mapping '{mapping}'");

        var propertyName = mapping["item.".Length..];

        if (ItemAccessor<TItem>.TryGetInt(propertyName, out var intGetter))
        {
            return (w, ctx) =>
                w.WriteNumberValue(intGetter((TItem)ctx.Item));
        }

        if (ItemAccessor<TItem>.TryGetString(propertyName, out var stringGetter))
        {
            return (w, ctx) =>
                w.WriteStringValue(stringGetter((TItem)ctx.Item));
        }

        if (ItemAccessor<TItem>.TryGetGuid(propertyName, out var guidGetter))
        {
            return (w, ctx) =>
                w.WriteStringValue(guidGetter((TItem)ctx.Item));
        }

        if (ItemAccessor<TItem>.TryGetDecimal(propertyName, out var decimalGetter))
        {
            return (w, ctx) =>
                w.WriteNumberValue(decimalGetter((TItem)ctx.Item));
        }

        throw new InvalidOperationException(
            $"Field '{propertyName}' not found on {typeof(TItem).Name}");
    }

    public void CompileArrayField<TItem>(
        IConfigurationSection field,
        List<JsonOp<TEvent, TRecord>> ops)
    {
        var contextPath = field["context"];
        if (string.IsNullOrEmpty(contextPath))
            throw new InvalidOperationException(
                $"Array field '{field.Key}' has no context");

        var enumerableGetter = CompileEnumerableGetter<TItem>(contextPath); // Func<TRecord, IEnumerable>

        var itemsSection = field.GetSection("items");
        if (!itemsSection.Exists())
            throw new InvalidOperationException(
                $"Array field '{field.Key}' has no items");

        var itemPlan = CompileItemPlan<TItem>(itemsSection);

        ops.Add(JsonOp<TEvent, TRecord>.NameOf(field.Key));
        ops.Add(JsonOp<TEvent, TRecord>.Emit((w, ctx) =>
        {
            var enumerable = enumerableGetter(ctx.Record);
            if (enumerable == null)
            {
                w.WriteStartArray();
                w.WriteEndArray();
                return;
            }

            w.WriteStartArray();

            foreach (var item in enumerable)
            {
                itemPlan.Execute(w, ctx, item);
            }

            w.WriteEndArray();
        }));
    }

    private JsonItemExecutionPlan<TEvent, TRecord, TItem> CompileItemPlan<TItem>(
    IConfigurationSection itemsSection)
    {
        var ops = new List<JsonOp<TEvent, TRecord>>();

        var fields = itemsSection.GetChildren();

        if (!fields.Any())
        {
            return new([]);
        }

        ops.Add(JsonOp<TEvent, TRecord>.ObjStart());

        foreach (var field in itemsSection.GetChildren())
        {
            CompileField<TItem>(field, ops);
        }

        ops.Add(JsonOp<TEvent, TRecord>.ObjEnd());

        return new JsonItemExecutionPlan<TEvent, TRecord, TItem>(ops.ToArray());
    }

    private void CompileField<TItem>(
        IConfigurationSection field,
        List<JsonOp<TEvent, TRecord>> ops)
    {
        if (field.Value != null)
        {
            // JSON property name
            ops.Add(JsonOp<TEvent, TRecord>.NameOf(field.Key));

            // scalar
            var emitter = CompileEmitterForItem<TItem>(field.Value);
            ops.Add(JsonOp<TEvent, TRecord>.Emit(emitter));
            return;
        }

        if (field["type"] == "array")
        {
            this.ArrayCompile<TEvent, TRecord, TItem>(field, ops);
            return;
        }

        if (field.Value is null)
        {
            // JSON property name
            ops.Add(JsonOp<TEvent, TRecord>.NameOf(field.Key));

            ops.Add(JsonOp<TEvent, TRecord>.ObjStart());
            foreach (var child in field.GetChildren())
            {
                CompileField<TItem>(child, ops);
            }
            ops.Add(JsonOp<TEvent, TRecord>.ObjEnd());
            return;
        }

        throw new InvalidOperationException($"Unknown field type: {field.Key}");
    }

    public void CompileArrayFieldForItem<TItem, TChildItem>(
        IConfigurationSection field,
        List<JsonOp<TEvent, TRecord>> ops)
    {
        var contextPath = field["context"];
        if (string.IsNullOrEmpty(contextPath))
            throw new InvalidOperationException(
                $"Array field '{field.Key}' has no context");

        var enumerableGetter = CompileEnumerableGetterForItem<TItem, TChildItem>(contextPath);

        var itemsSection = field.GetSection("items");
        if (!itemsSection.Exists())
            throw new InvalidOperationException(
                $"Array field '{field.Key}' has no items");

        var itemPlan = CompileItemPlan<TChildItem>(itemsSection);

        ops.Add(JsonOp<TEvent, TRecord>.NameOf(field.Key));
        ops.Add(JsonOp<TEvent, TRecord>.Emit((w, ctx) =>
        {
            var enumerable = enumerableGetter((TItem)ctx.Item);
            if (enumerable == null)
            {
                w.WriteStartArray();
                w.WriteEndArray();
                return;
            }

            w.WriteStartArray();

            foreach (var childItem in enumerable)
            {
                itemPlan.Execute(w, ctx, childItem);
            }

            w.WriteEndArray();
        }));
    }

    private static Func<TItem, IEnumerable<TChildItem>> CompileEnumerableGetterForItem<TItem, TChildItem>(string mapping)
    {
        if (!mapping.StartsWith("item.", StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Array context for item-plan must start with 'item.': {mapping}");

        var path = mapping["item.".Length..];

        if (!ItemAccessor<TItem>.TryGetEnumerable<TChildItem>(path, out var getter))
            throw new InvalidOperationException(
                $"Enumerable '{path}' not found on {typeof(TItem).Name}");

        return getter;
    }

    private static Action<Utf8JsonWriter, JsonExecutionContext<TEvent, TRecord>> CompileEmitterForItem<TItem>(string mapping)
    {
        if (mapping.StartsWith("item."))
        {
            return CompileItemEmitter<TItem>(mapping);
        }

        if (mapping.StartsWith("record."))
        {
            return CompileRecordEmitter(mapping);
        }

        throw new NotSupportedException(mapping);
    }

    private static Func<TRecord, IEnumerable<TItem>> CompileEnumerableGetter<TItem>(string mapping)
    {
        if (!mapping.StartsWith("record.", StringComparison.Ordinal))
            throw new NotSupportedException(
                $"Array context must start with 'record.': {mapping}");

        var path = mapping["record.".Length..];

        if (!RecordAccessor<TRecord>.TryGetEnumerable<TItem>(path, out var getter))
            throw new InvalidOperationException(
                $"Enumerable '{path}' not found on {typeof(TRecord).Name}");

        return getter;
    }
}
