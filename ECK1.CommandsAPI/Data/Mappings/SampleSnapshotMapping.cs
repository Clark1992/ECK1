using ECK1.CommandsAPI.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.CommandsAPI.Data.Mappings;

public class SampleSnapshotMapping : IEntityTypeConfiguration<SampleSnapshotEntity>
{
    public void Configure(EntityTypeBuilder<SampleSnapshotEntity> builder)
    {
        builder.ToTable("SampleSnapshots");

        builder.HasKey(x => x.SnapshotId);

        builder.Property(x => x.SampleId).IsRequired();
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.SnapshotData).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.SampleId, x.Version }).IsUnique();
    }
}