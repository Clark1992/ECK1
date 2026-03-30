using Microsoft.EntityFrameworkCore;

namespace ECK1.Reconciler.Data;

public class ReconcilerDbContext(DbContextOptions<ReconcilerDbContext> options) : DbContext(options)
{
    public DbSet<Models.EntityState> EntityStates => Set<Models.EntityState>();
    public DbSet<Models.ReconcileFailure> ReconcileFailures => Set<Models.ReconcileFailure>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ReconcilerDbContext).Assembly);
    }
}
