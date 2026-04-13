namespace ECK1.QueriesAPI.Queries;

public record EntityResponse<T>(T Data, bool IsRebuilding);
