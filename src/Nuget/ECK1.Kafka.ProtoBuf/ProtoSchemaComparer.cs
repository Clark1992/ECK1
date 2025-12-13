extern alias ProtobufNetReflection;
using System.Text;
using ProtobufNet = ProtobufNetReflection::Google.Protobuf.Reflection;

namespace ECK1.Kafka.ProtoBuf;

public class ProtoSchemaComparer
{
    private static readonly string _tmpName = "temp.proto";

    private static (bool isSuccess, string error) Success => (true, null);
    private static (bool isSuccess, string error) Error(string error) => (false, error);

    /// <summary>
    /// Compare two .proto schema definitions and return true when they are compatible
    /// (A <= B, where A is the expected schema and B is the one from registry).
    /// Logging is used for details.
    /// </summary>
    public static (bool isSuccess, string error) Compare(string protoA, string protoB)
    {
        var setA = LoadSchema(protoA);
        var setB = LoadSchema(protoB);

        var messagesA = GetAllMessages(setA);
        var messagesB = GetAllMessages(setB);

        foreach (var msgA in messagesA)
        {
            var msgB = messagesB.FirstOrDefault(m => m.Name == msgA.Name);
            if (msgB == null)
            {
                return Error($"Missing message in B: { msgA.Name}");
            }

            return CompareMessage(msgA, msgB, string.Empty);
        }

        // find extra messages in B
        foreach (var msgB in messagesB)
        {
            if (!messagesA.Any(m => m.Name == msgB.Name))
            {
                return Error($"Extra message in B: {msgB.Name}");
            }
        }

        return Success;
    }

    private static ProtobufNet.FileDescriptorSet LoadSchema(string proto)
    {
        var set = new ProtobufNet.FileDescriptorSet();
        set.Add(_tmpName, false, new StringReader(proto));
        set.Process();
        if (set.GetErrors().Any())
            throw new InvalidOperationException(
                "Schema parse errors: " +
                string.Join(", ", set.GetErrors().Select(e => e.ToString())));
        return set;
    }

    private static List<ProtobufNet.DescriptorProto> GetAllMessages(ProtobufNet.FileDescriptorSet set) =>
        [.. set.Files.Single(x => x.Name == _tmpName).MessageTypes.Select(ConvertToProto)];

    private static ProtobufNet.DescriptorProto ConvertToProto(ProtobufNet.DescriptorProto src)
    {
        var result = new ProtobufNet.DescriptorProto { Name = src.Name };
        result.Fields.AddRange(src.Fields.Select(
            f => new ProtobufNet.FieldDescriptorProto
            {
                Name = f.Name,
                Number = f.Number,
                TypeName = f.TypeName,
                label = f.label,
                type = f.type
            }));

        foreach (var nested in src.NestedTypes)
            result.NestedTypes.Add(ConvertToProto(nested));

        return result;
    }

    private static (bool isSuccess, string error) CompareMessage(ProtobufNet.DescriptorProto a, ProtobufNet.DescriptorProto b, string path)
    {
        var fullName = string.IsNullOrEmpty(path) ? a.Name : $"{path}.{a.Name}";

        // compare fields from A against B
        foreach (var fa in a.Fields)
        {
            var fb = b.Fields.FirstOrDefault(f => f.Number == fa.Number);
            if (fb == null)
            {
                return Error($"Missing field #{fa.Number} '{fa.Name}' in B for message {fullName}");
            }

            bool nameMatch = fa.Name == fb.Name;
            // Type considered equal if TypeName matches (for messages/enums) OR numeric type value matches (for primitives)
            bool typeMatch = (fa.TypeName == fb.TypeName) || (fa.type == fb.type);
            bool repeatedMatch = fa.label == fb.label;

            if (!nameMatch || !typeMatch || !repeatedMatch)
            {
                var errorSb = new StringBuilder($"Field #{fa.Number} mismatch in {fullName}");
                if (!nameMatch)
                    errorSb.Append($"   name: A='{fa.Name}' vs B='{fb.Name}'");
                if (!typeMatch)
                    errorSb.Append($"   type: A='{fa.TypeName?? fa.type.ToString()}' vs B='{fb.TypeName ?? fb.type.ToString()}'");
                if (!repeatedMatch)
                    errorSb.Append($"   repeated: A={fa.label} vs B={fb.label}");

                return Error(errorSb.ToString());
            }
        }

        // find extra fields in B
        foreach (var fb in b.Fields)
        {
            if (!a.Fields.Any(f => f.Number == fb.Number))
            {
                return Error($"Extra field in B: #{fb.Number} '{fb.Name}' in message {b.Name}");
            }
        }

        // compare nested types
        foreach (var nestedA in a.NestedTypes)
        {
            var nestedB = b.NestedTypes.FirstOrDefault(n => n.Name == nestedA.Name);
            if (nestedB == null)
            {
                return Error($"Missing nested message {nestedA.Name} in B for message {fullName}");
            }

            return CompareMessage(nestedA, nestedB, fullName);
        }

        return Success;
    }
}