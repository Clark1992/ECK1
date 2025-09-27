using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;

namespace ECK1.Contracts.Shared;

/// <summary>
/// Adapter to mimic System.Text JsonPolymorphic shape with Newtonsoft.Json
/// </summary>
/// <typeparam name="T"></typeparam>
public class Polymorph<T> : JsonConverter
{
    private static HashSet<Type> DerivedTypesSet { get; } = new();
    private static Dictionary<string, Type> DerivedTypes { get; } = new();
    private static string Discriminator { get; set; }

    public Polymorph(string typeDiscriminator)
    {
        Discriminator = typeDiscriminator;
    }

    static Polymorph()
    {
        PopulateDerivedTypes();
    }

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var jsonObject = JObject.Load(reader);

        var typeName = jsonObject[Discriminator]?.ToString();
        if (typeName == null)
            throw new JsonSerializationException($"Missing discriminator '{Discriminator}'");

        if (!DerivedTypes.TryGetValue(typeName, out var targetType))
            throw new JsonSerializationException($"Unknown $type: {typeName}");

#pragma warning disable SYSLIB0050 // Type or member is obsolete
        var obj = (T)FormatterServices.GetUninitializedObject(targetType);
#pragma warning restore SYSLIB0050 // Type or member is obsolete

        serializer.Populate(jsonObject.CreateReader(), obj);

        return obj;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        var isRoot = writer is not JTokenWriter && writer.Path == string.Empty;

        writer.WriteStartObject();

        var type = value.GetType();
        if (isRoot)
        {
            writer.WritePropertyName("$type");
            writer.WriteValue(type.Name);
        }

        var contract = (JsonObjectContract)serializer.ContractResolver.ResolveContract(type);
        foreach (var prop in contract.Properties.Where(p => p.Readable && !p.Ignored))
        {
            var propValue = prop.ValueProvider.GetValue(value);
            if (propValue == null && serializer.NullValueHandling == NullValueHandling.Ignore)
                continue;

            writer.WritePropertyName(prop.PropertyName);
            serializer.Serialize(writer, propValue);
        }

        writer.WriteEndObject();
    }

    public override bool CanConvert(Type objectType) => DerivedTypesSet.Contains(objectType);

    private static void PopulateDerivedTypes()
    {
        var baseType = typeof(T);

        foreach (System.Text.Json.Serialization.JsonDerivedTypeAttribute attr in baseType.GetCustomAttributes<System.Text.Json.Serialization.JsonDerivedTypeAttribute>(inherit: false))
        {
            if (baseType.IsAssignableFrom(attr.DerivedType))
            {
                DerivedTypes[attr.TypeDiscriminator.ToString()] = attr.DerivedType;
                DerivedTypesSet.Add(attr.DerivedType);
            }
        }
    }
}
