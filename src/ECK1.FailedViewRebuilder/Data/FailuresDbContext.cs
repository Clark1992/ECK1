using ECK1.FailedViewRebuilder.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace ECK1.FailedViewRebuilder.Data;

public class FailuresDbContext : DbContext
{
    public FailuresDbContext(DbContextOptions<FailuresDbContext> options) : base(options)
    {
    }

    public DbSet<SampleEventFailure> SampleEventFailures { get; set; }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FailuresDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}