using ECK1.CommandsAPI.Data.Models;
using ECK1.CommandsAPI.Domain;
using Microsoft.EntityFrameworkCore;

namespace ECK1.CommandsAPI.Data;

public class CommandsDbContext : DbContext
{
    public CommandsDbContext(DbContextOptions<CommandsDbContext> options) : base(options)
    {
    }

    public DbSet<SampleEventEntity> SampleEvents { get; set; }
    public DbSet<SampleSnapshotEntity> SampleSnapshots { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommandsDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}