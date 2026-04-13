using ProtoBuf;
using System.ServiceModel;

namespace ECK1.VersionTracker.Contracts;

[ServiceContract]
public interface IVersionTrackerService
{
    [OperationContract]
    ValueTask<PutVersionResponse> PutVersion(PutVersionRequest request);

    [OperationContract]
    ValueTask<GetVersionResponse> GetVersion(GetVersionRequest request);
}

[ProtoContract]
public class PutVersionRequest
{
    [ProtoMember(1)] public string EntityType { get; set; } = string.Empty;
    [ProtoMember(2)] public string EntityId { get; set; } = string.Empty;
    [ProtoMember(3)] public int Version { get; set; }
}

[ProtoContract]
public class PutVersionResponse { }

[ProtoContract]
public class GetVersionRequest
{
    [ProtoMember(1)] public string EntityType { get; set; } = string.Empty;
    [ProtoMember(2)] public string EntityId { get; set; } = string.Empty;
    [ProtoMember(3)] public int ExpectedVersion { get; set; }
}

[ProtoContract]
public class GetVersionResponse
{
    [ProtoMember(1)] public int Version { get; set; }
}

public static class VersionTrackerConstants
{
    public const string TargetName = "VersionTracker";
}
