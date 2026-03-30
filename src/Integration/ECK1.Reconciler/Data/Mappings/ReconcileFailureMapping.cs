using ECK1.Reconciler.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.Reconciler.Data.Mappings;

public class ReconcileFailureMapping : IEntityTypeConfiguration<ReconcileFailure>
{
    public void Configure(EntityTypeBuilder<ReconcileFailure> builder)
    {
        builder.ToTable("ReconcileFailures", "dbo");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedOnAdd();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.EntityType)
            .HasColumnType("nvarchar(128)")
            .IsRequired();

        builder.Property(x => x.FailedPlugin)
            .HasColumnType("nvarchar(64)")
            .IsRequired();

        builder.Property(x => x.IsFullHistoryRebuild)
            .IsRequired();

        builder.Property(x => x.FailedAt)
            .IsRequired();

        builder.Property(x => x.DispatchedAt);

        builder.HasIndex(x => x.DispatchedAt)
            .HasDatabaseName("IX_ReconcileFailures_DispatchedAt");

        builder.HasIndex(x => new { x.EntityId, x.EntityType, x.FailedPlugin })
            .HasDatabaseName("IX_ReconcileFailures_Entity");
    }
}
