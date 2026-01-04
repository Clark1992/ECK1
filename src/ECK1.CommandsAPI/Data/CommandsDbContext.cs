using ECK1.CommandsAPI.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ECK1.CommandsAPI.Data;

public class CommandsDbContext : DbContext
{
    public CommandsDbContext(DbContextOptions<CommandsDbContext> options) : base(options)
    {
    }

    public DbSet<SampleEventEntity> SampleEvents { get; set; }
    public DbSet<SampleSnapshotEntity> SampleSnapshots { get; set; }

    public DbSet<Sample2EventEntity> Sample2Events { get; set; }
    public DbSet<Sample2SnapshotEntity> Sample2Snapshots { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommandsDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}