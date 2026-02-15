using RocksDbSharp;

namespace ECK1.Integration.Cache.ShortTerm;

public class KafkaSettings
{
    public static string Section => "Kafka";
    public string BootstrapServers { get; set; }
    public string SchemaRegistryUrl { get; set; }
    public string User { get; set; }
    public string Secret { get; set; }
    public string CacheProgressTopic { get; set; }
    public string GroupId { get; set; } = Guid.NewGuid().ToString();
}

public class MemoryCacheConfig
{
    public int ExpirationMinutes { get; set; }
    public long EntrySize { get; set; } = 1;
}

public class RocksDbConfig
{
    public int MaxOpenFiles { get; set; } = 256;
    public int WriteBufferSizeMb { get; set; } = 32;
    public Compression Compression { get; set; } = Compression.No;
    public string DbPath { get; set; } = "/data/rocksdb";
}

public class CacheConfig
{
    public static string Section => "Cache";

    public MemoryCacheConfig Memory { get; set; } = new();

    public RocksDbConfig RocksDb {  get; set; } = new();
}