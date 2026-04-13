#nullable enable
using System.Collections.Concurrent;

namespace ECK1.Gateway.Realtime;

public interface IRealtimeConnectionManager
{
    void AddConnection(string connectionId, string userId);
    void RemoveConnection(string connectionId);
    string? GetUserIdByConnection(string connectionId);
}

public class RealtimeConnectionManager : IRealtimeConnectionManager
{
    private readonly ConcurrentDictionary<string, string> _connectionToUser = new();

    public void AddConnection(string connectionId, string userId)
    {
        _connectionToUser[connectionId] = userId;
    }

    public void RemoveConnection(string connectionId)
    {
        _connectionToUser.TryRemove(connectionId, out _);
    }

    public string? GetUserIdByConnection(string connectionId)
    {
        return _connectionToUser.GetValueOrDefault(connectionId);
    }
}
