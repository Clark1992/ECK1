using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text;
using ProtoBuf;
using ECK1.Integration.Common;
using ECK1.Integration.EntityStore.Generated;
using RocksDbSharp;

namespace ECK1.Integration.Cache.ShortTerm.Services;

public class EntityStore : IDisposable, IEntityStore
{
    private readonly IMemoryCache _memory;
    private readonly CacheConfig _config;
    private readonly ILogger<EntityStore> _logger;

    private RocksDb _db;
    private readonly object _dbLock = new();
    private bool _disposed;

    public EntityStore(IOptions<CacheConfig> config, IMemoryCache memory, ILogger<EntityStore> logger)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;

        EnsureDbPath(_config.RocksDb.DbPath);
        _db = OpenDb(_config.RocksDb.DbPath);
    }

    private static void EnsureDbPath(string path)
    {
        if (!Directory.Exists(path)) Directory.CreateDirectory(path);
    }

    private RocksDb OpenDb(string path)
    {
        var optsHandle = new DbOptions()
            .SetCreateIfMissing(true)
            .SetCreateMissingColumnFamilies(true)
            .SetMaxOpenFiles(_config.RocksDb.MaxOpenFiles)
            .SetWriteBufferSize((ulong)_config.RocksDb.WriteBufferSizeMb * 1024 * 1024)
            .SetCompression(_config.RocksDb.Compression);

        return RocksDb.Open(optsHandle, path);
    }

    public void Put<T>(string key, int version, T obj) where T : class
    {
        var fullKey = BuildKey(typeof(T).FullName, key);

        lock (_dbLock)
        {
            DisposeGuard();

            try
            {
                if (obj == null)
                {
                    Remove(fullKey);
                    return;
                }

                var entry = new EntityEntry<T> { Version = version, Item = obj };
                var bytes = EntitySerializer.ToBytes(entry);

                _db.Put(Encoding.UTF8.GetBytes(fullKey), bytes);

                SaveInMemoryCache(fullKey, entry, version);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Put failed for key {Key}", fullKey);
                throw;
            }
        }
    }

    private void Remove(string fullKey)
    {
        _db.Remove(Encoding.UTF8.GetBytes(fullKey));
        _memory.Remove(fullKey);
        _logger.LogInformation("Removed {FullKey}", fullKey);
    }

    public void PutMany<T>(IEnumerable<(string key, int version, T obj)> items)
    {
        if (items == null) return;

        lock (_dbLock)
        {
            DisposeGuard();

            using var wb = new WriteBatch();
            try
            {
                foreach (var (key, version, obj) in items)
                {
                    var fullKey = BuildKey(typeof(T).FullName, key);

                    if (obj == null)
                    {
                        Remove(fullKey);
                        continue;
                    }

                    var entry = new EntityEntry<T> { Version = version, Item = obj };
                    wb.Put(Encoding.UTF8.GetBytes(fullKey), EntitySerializer.ToBytes(entry));
                    SaveInMemoryCache(fullKey, entry, version);
                }

                _db.Write(wb);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "PutMany failed");
                throw;
            }
        }
    }

    public EntityEntry<T> Get<T>(string key, int minVersion) where T : class
    {
        var fullKey = BuildKey(typeof(T).FullName, key);

        if (_memory.TryGetValue<EntityEntry<T>>(fullKey, out var cached))
            return cached.Version >= minVersion ? cached : null;

        cached = GetEntryFromDb<T>(fullKey);

        if (cached == null) return null;
        if (cached.Version < minVersion)
        {
            _logger.LogWarning("Entry {FullKey} version is stale. Actual: {Actual} < Expected: {Expected}",
                fullKey, cached.Version, minVersion);
            return null;
        }

        return cached;
    }

    public void ClearAll()
    {
        lock (_dbLock)
        {
            DisposeGuard();

            _db.Dispose();

            var path = _config.RocksDb.DbPath;
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }

            Directory.CreateDirectory(path);
            _db = OpenDb(path);
        }
    }

    public void Dispose()
    {
        lock (_dbLock)
        {
            if (_disposed) return;
            try { _db?.Dispose(); }
            catch (Exception ex) { _logger?.LogWarning(ex, "Dispose RocksDB failed"); }
            _disposed = true;
        }
    }

    private void SaveInMemoryCache<T>(string key, T entry, int version) =>
        SaveInMemoryCache(key, new EntityEntry<T> { Item = entry, Version = version });

    private void SaveInMemoryCache<T>(string key, T entry)
    {
        try
        {
            var opts = new MemoryCacheEntryOptions
            {
                SlidingExpiration = TimeSpan.FromMinutes(_config.Memory.ExpirationMinutes),
                Size = _config.Memory.EntrySize
            };
            _memory.Set(key, entry, opts);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "MemoryCache set failed for key {Key}", key);
        }
    }

    private EntityEntry<T> GetEntryFromDb<T>(string fullKey)
    {
        lock (_dbLock)
        {
            DisposeGuard();

            var bytes = _db.Get(Encoding.UTF8.GetBytes(fullKey));
            if (bytes == null) return null;

            var stored = EntitySerializer.FromBytes<EntityEntry<T>>(bytes);
            if (stored != null)
                SaveInMemoryCache(fullKey, stored);

            return stored;
        }
    }

    private static string BuildKey(string entityType, string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(entityType);
        ArgumentException.ThrowIfNullOrEmpty(key);

        return $"{entityType}:{key}";
    }

    private void DisposeGuard() => ObjectDisposedException.ThrowIf(_disposed, nameof(EntityStore));
}
