namespace ECK1.CodeGen.Shared.Grpc;

internal static class GrpcCommon
{
    internal static readonly string NsPrefix = "ECK1.Integration.EntityStore";
    internal static string BuildGrpcGetEntityMethodName(string safeId) => $"Get_{safeId}_Entity";
}
