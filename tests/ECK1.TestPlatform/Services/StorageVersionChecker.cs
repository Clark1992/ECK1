using System.Text.Json;

namespace ECK1.TestPlatform.Services;

/// <summary>
/// Reusable helper that checks entity version state across storage targets (Mongo, ES, Clickhouse)
/// by parsing QueriesAPI JSON responses.
/// </summary>
public sealed class StorageVersionChecker(QueriesApiClient queriesClient)
{
    private static readonly HashSet<string> VerifiableTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        "mongo", "elasticsearch", "clickhouse"
    };

    public static bool IsVerifiable(string target) => VerifiableTargets.Contains(target);

    public static IReadOnlySet<string> AllVerifiableTargets => VerifiableTargets;

    /// <summary>
    /// Gets the current version of an entity in the specified target storage.
    /// Returns null if the entity is missing or the target is not queryable.
    /// </summary>
    public async Task<int?> GetEntityVersionAsync(string target, Guid entityId, CancellationToken ct)
    {
        if (target.Equals("mongo", StringComparison.OrdinalIgnoreCase))
            return GetVersionFromMongo(await queriesClient.GetSampleByIdAsync(entityId, ct));

        if (target.Equals("elasticsearch", StringComparison.OrdinalIgnoreCase))
            return GetVersionFromEs(await queriesClient.SearchSampleByIdAsync(entityId, ct), entityId);

        if (target.Equals("clickhouse", StringComparison.OrdinalIgnoreCase))
            return GetMaxVersionFromClickhouse(await queriesClient.GetSampleHistoryAsync(entityId, ct));

        return null;
    }

    /// <summary>
    /// For Clickhouse: checks whether ALL versions from 1..expectedVersion are present (no gaps).
    /// Returns true only when the complete version sequence exists.
    /// </summary>
    public async Task<bool> AllVersionsPresentInClickhouseAsync(Guid entityId, int expectedVersion, CancellationToken ct)
    {
        JsonElement? response = await queriesClient.GetSampleHistoryAsync(entityId, ct);
        if (!response.HasValue || response.Value.ValueKind != JsonValueKind.Array)
            return false;

        var versions = new HashSet<int>();
        foreach (JsonElement item in response.Value.EnumerateArray())
        {
            if (item.TryGetProperty("entityVersion", out JsonElement versionProp) &&
                versionProp.TryGetInt32(out int v))
            {
                versions.Add(v);
            }
        }

        for (int v = 1; v <= expectedVersion; v++)
        {
            if (!versions.Contains(v))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether an entity is "healed" in the given target at the expected version.
    /// For Clickhouse this additionally verifies all intermediate versions are present.
    /// For Mongo/ES this checks that the stored version >= expectedVersion.
    /// </summary>
    public async Task<bool> IsEntityHealedAsync(string target, Guid entityId, int expectedVersion, CancellationToken ct)
    {
        if (target.Equals("clickhouse", StringComparison.OrdinalIgnoreCase))
            return await AllVersionsPresentInClickhouseAsync(entityId, expectedVersion, ct);

        int? actualVersion = await GetEntityVersionAsync(target, entityId, ct);
        return actualVersion is not null && actualVersion.Value >= expectedVersion;
    }

    // ── JSON parsing helpers ──────────────────────────────────────────

    /// <summary>
    /// Mongo GET: { data: { version: N, ... }, isRebuilding: ... }
    /// </summary>
    public static int? GetVersionFromMongo(JsonElement? response)
    {
        if (!response.HasValue) return null;
        if (response.Value.TryGetProperty("data", out JsonElement data) &&
            data.TryGetProperty("version", out JsonElement version) &&
            version.TryGetInt32(out int v))
            return v;
        return null;
    }

    /// <summary>
    /// ES search: { items: [{ sampleId: "...", version: N }], total: N }
    /// </summary>
    public static int? GetVersionFromEs(JsonElement? response, Guid entityId)
    {
        if (!response.HasValue) return null;
        if (!response.Value.TryGetProperty("items", out JsonElement items)) return null;
        foreach (JsonElement item in items.EnumerateArray())
        {
            if (item.TryGetProperty("sampleId", out JsonElement idProp) &&
                Guid.TryParse(idProp.GetString(), out Guid id) && id == entityId &&
                item.TryGetProperty("version", out JsonElement version) &&
                version.TryGetInt32(out int v))
            {
                return v;
            }
        }
        return null;
    }

    /// <summary>
    /// Clickhouse history: [{ entityVersion: N, ... }, ...]  — returns max version.
    /// </summary>
    public static int? GetMaxVersionFromClickhouse(JsonElement? response)
    {
        if (!response.HasValue) return null;
        if (response.Value.ValueKind != JsonValueKind.Array) return null;
        int? maxVersion = null;
        foreach (JsonElement item in response.Value.EnumerateArray())
        {
            if (item.TryGetProperty("entityVersion", out JsonElement versionProp) &&
                versionProp.TryGetInt32(out int v))
            {
                maxVersion = maxVersion is null ? v : Math.Max(maxVersion.Value, v);
            }
        }
        return maxVersion;
    }
}
