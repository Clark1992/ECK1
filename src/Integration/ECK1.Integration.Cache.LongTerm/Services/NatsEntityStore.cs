using System.Collections.Concurrent;
using System.Diagnostics;
using ECK1.Integration.Common;
using ECK1.Integration.EntityStore.Generated;
using Microsoft.Extensions.Options;
using NATS.Client.Core;
using NATS.Client.JetStream;
using NATS.Client.KeyValueStore;

namespace ECK1.Integration.Cache.LongTerm.Services;

public class NatsEntityStore : IDisposable, IEntityStore
{
    private readonly NatsSettings _config;
    private readonly ILogger<NatsEntityStore> _logger;
    private readonly NatsConnection _connection;
    private readonly NatsJSContext _jsContext;
    private readonly NatsKVContext _kvContext;
    private readonly INatsTelemetry _natsTelemetry;
    private readonly ConcurrentDictionary<string, INatsKVStore> _stores = new();
    private readonly Lazy<Task> _connectTask;
    private readonly object _storeLock = new();
    private bool _disposed;

    public NatsEntityStore(IOptions<NatsSettings> config, ILogger<NatsEntityStore> logger, INatsTelemetry natsTelemetry)
    {
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
        _natsTelemetry = natsTelemetry ?? throw new ArgumentNullException(nameof(natsTelemetry));

        var opts = new NatsOpts
        {
            Url = _config.Url,
        };

        _connection = new NatsConnection(opts);
        _jsContext = new NatsJSContext(_connection);
        _kvContext = new NatsKVContext(_jsContext);
        _connectTask = new Lazy<Task>(() => _connection.ConnectAsync().AsTask());
    }

    public void Put<T>(string key, int version, T obj) where T : class
    {
        var store = GetStoreForType(typeof(T), out var bucket);
        using var activity = _natsTelemetry.Start("nats.kv.put", obj is null ? "delete" : "put", bucket, key);

        try
        {
            if (obj == null)
            {
                store.DeleteAsync(key).AsTask().GetAwaiter().GetResult();
                return;
            }

            var entry = new EntityEntry<T> { Version = version, Item = obj };
            var bytes = EntitySerializer.ToBytes(entry);

            store.PutAsync(key, bytes).AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _natsTelemetry.SetError(activity, ex);
            _logger?.LogError(ex, "Put failed for key {Key}", key);
            throw;
        }
    }

    public void PutMany<T>(IEnumerable<(string key, int version, T obj)> items)
    {
        if (items == null) return;

        var store = GetStoreForType(typeof(T), out var bucket);
        using var activity = _natsTelemetry.Start("nats.kv.put_many", "put_many", bucket);
        var count = 0;

        foreach (var (key, version, obj) in items)
        {
            count++;
            try
            {
                if (obj == null)
                {
                    store.DeleteAsync(key).AsTask().GetAwaiter().GetResult();
                    continue;
                }

                var entry = new EntityEntry<T> { Version = version, Item = obj };
                var bytes = EntitySerializer.ToBytes(entry);

                store.PutAsync(key, bytes).AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _natsTelemetry.SetError(activity, ex);
                _logger?.LogError(ex, "PutMany failed for key {Key}", key);
                throw;
            }
        }

        _natsTelemetry.SetCount(activity, count);
    }

    public EntityEntry<T> Get<T>(string key, int minVersion) where T : class
    {
        var store = GetStoreForType(typeof(T), out var bucket);
        using var activity = _natsTelemetry.Start("nats.kv.get", "get", bucket, key);

        try
        {
            var entry = store.GetEntryAsync<byte[]>(key).AsTask().GetAwaiter().GetResult();
            if (entry.Value == null) return null;

            var stored = EntitySerializer.FromBytes<EntityEntry<T>>(entry.Value);
            if (stored == null) return null;

            if (stored.Version < minVersion)
            {
                _logger.LogWarning("Entry {Key} version is stale. Actual: {Actual} < Expected: {Expected}",
                    key, stored.Version, minVersion);
                return null;
            }

            return stored;
        }
        catch (NatsKVKeyNotFoundException)
        {
            _natsTelemetry.SetMiss(activity);
            return null;
        }
        catch (NatsKVKeyDeletedException)
        {
            _natsTelemetry.SetMiss(activity);
            return null;
        }
        catch (Exception ex)
        {
            _natsTelemetry.SetError(activity, ex);
            _logger?.LogError(ex, "Get failed for key {Key}", key);
            throw;
        }
    }

    public void ClearAll()
    {
        EnsureConnected();

        foreach (var bucket in _config.BucketsByEntityType.Values.Distinct(StringComparer.Ordinal))
        {
            using var activity = _natsTelemetry.Start("nats.kv.delete_bucket", "delete_bucket", bucket);
            _kvContext.DeleteStoreAsync(bucket).AsTask().GetAwaiter().GetResult();
        }

        _stores.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _connection.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Dispose NATS connection failed");
        }
    }

    private INatsKVStore GetStoreForType(Type entityType, out string bucket)
    {
        ArgumentException.ThrowIfNullOrEmpty(entityType.Name);

        if (!_config.BucketsByEntityType.TryGetValue(entityType.Name, out bucket))
        {
            throw new InvalidOperationException($"NATS KV bucket not configured for entity type '{entityType}'.");
        }

        return GetOrCreateStore(bucket);
    }

    private INatsKVStore GetOrCreateStore(string bucket)
    {
        if (_stores.TryGetValue(bucket, out var store)) return store;

        lock (_storeLock)
        {
            if (_stores.TryGetValue(bucket, out store)) return store;

            EnsureConnected();

            using var activity = _natsTelemetry.Start("nats.kv.create_bucket", "create_bucket", bucket);

            var config = new NatsKVConfig(bucket)
            {
                History = _config.MaxHistory,
                Storage = NatsKVStorageType.File
            };

            store = _kvContext.CreateOrUpdateStoreAsync(config).AsTask().GetAwaiter().GetResult();
            _stores.TryAdd(bucket, store);
            return store;
        }
    }

    private void EnsureConnected()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(NatsEntityStore));
        }

        _connectTask.Value.GetAwaiter().GetResult();
    }
}
