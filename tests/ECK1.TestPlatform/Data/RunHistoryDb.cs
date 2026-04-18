using Microsoft.EntityFrameworkCore;

namespace ECK1.TestPlatform.Data;

public sealed class RunHistoryDb(DbContextOptions<RunHistoryDb> options) : DbContext(options)
{
    public DbSet<ScenarioRunRecord> Runs => Set<ScenarioRunRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ScenarioRunRecord>(e =>
        {
            e.HasKey(r => r.RunId);
            e.Property(r => r.RunId).HasMaxLength(64);
            e.Property(r => r.ScenarioId).HasMaxLength(128);
            e.Property(r => r.ScenarioName).HasMaxLength(256);
            e.HasIndex(r => r.StartedAt);
            e.HasIndex(r => r.IsCompleted);
        });
    }
}

public sealed class ScenarioRunRecord
{
    public string RunId { get; set; } = "";
    public string ScenarioId { get; set; } = "";
    public string ScenarioName { get; set; } = "";
    public bool IsCompleted { get; set; }
    public bool IsSuccess { get; set; }
    public string? Error { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string SettingsJson { get; set; } = "{}";
    public string ProgressJson { get; set; } = "{}";
}
