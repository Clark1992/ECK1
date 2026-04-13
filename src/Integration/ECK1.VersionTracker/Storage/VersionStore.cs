using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

namespace ECK1.VersionTracker.Storage;

public class VersionStore
{
    private readonly IMongoCollection<VersionEntry> _collection;
    private readonly IMemoryCache _cache;
    private readonly ILogger<VersionStore> _logger;

    private static readonly MemoryCacheEntryOptions CacheOptions = new MemoryCacheEntryOptions()
        .SetSlidingExpiration(TimeSpan.FromMinutes(30))
        .SetSize(1);

    public VersionStore(MongoDbSettings mongoSettings, ILogger<VersionStore> logger)
    {
        _logger = logger;

        var connectionString = mongoSettings.ConnectionString
            ?? throw new InvalidOperationException("MongoDb:ConnectionString is not configured.");
        var databaseName = mongoSettings.DatabaseName
            ?? throw new InvalidOperationException("MongoDb:DatabaseName is not configured.");

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        _collection = database.GetCollection<VersionEntry>("entity_versions");

        _cache = new MemoryCache(new MemoryCacheOptions
        {
            SizeLimit = 50_000
        });

        logger.LogInformation("VersionStore initialized with MongoDB backend and LRU cache (limit=50000)");
    }

    private static string BuildKey(string entityType, string entityId) => $"{entityType}:{entityId}";

    public async Task<bool> PutAsync(string entityType, string entityId, int version)
    {
        var key = BuildKey(entityType, entityId);

        try
        {
            var filter = Builders<VersionEntry>.Filter.Eq(e => e.Key, key)
                & Builders<VersionEntry>.Filter.Lt(e => e.Version, version);

            var update = Builders<VersionEntry>.Update
                .Set(e => e.Version, version)
                .SetOnInsert(e => e.Key, key);

            var result = await _collection.UpdateOneAsync(
                filter,
                update,
                new UpdateOptions { IsUpsert = true });

            _cache.Set(key, version, CacheOptions);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Another instance set a higher version concurrently — that's fine
            _logger.LogDebug("Concurrent higher version already set for {Key}", key);
            _cache.Remove(key);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist version for {Key}", key);
            return false;
        }
    }

    public async Task<int> GetAsync(string entityType, string entityId)
    {
        var key = BuildKey(entityType, entityId);

        if (_cache.TryGetValue<int>(key, out var cached))
            return cached;

        try
        {
            var entry = await _collection
                .Find(Builders<VersionEntry>.Filter.Eq(e => e.Key, key))
                .FirstOrDefaultAsync();

            var version = entry?.Version ?? 0;
            _cache.Set(key, version, CacheOptions);
            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read version for {Key}", key);
            return 0;
        }
    }
}

public class VersionEntry
{
    public string Key { get; set; } = string.Empty;
    public int Version { get; set; }
}
