using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.Reconciler.Data.Mappings;

public class EntityStateMapping : IEntityTypeConfiguration<Models.EntityState>
{
    public void Configure(EntityTypeBuilder<Models.EntityState> builder)
    {
        builder.ToTable("EntityState", "dbo");
        builder.HasKey(x => new { x.EntityId, x.EntityType });

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasColumnType("nvarchar(128)")
            .IsRequired();

        builder.Property(x => x.ExpectedVersion)
            .IsRequired();

        builder.Property(x => x.LastEventOccuredAt)
            .IsRequired();

        builder.Property(x => x.ReconciledAt);

        builder.HasIndex(x => x.ReconciledAt)
            .HasDatabaseName("IX_EntityState_ReconciledAt");
    }
}
