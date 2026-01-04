using ECK1.CommandsAPI.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.CommandsAPI.Data.Mappings;

public class Sample2SnapshotMapping : IEntityTypeConfiguration<Sample2SnapshotEntity>
{
    public void Configure(EntityTypeBuilder<Sample2SnapshotEntity> builder)
    {
        builder.ToTable("Sample2Snapshots");

        builder.HasKey(x => x.SnapshotId);

        builder.Property(x => x.Sample2Id).IsRequired();
        builder.Property(x => x.Version).IsRequired();
        builder.Property(x => x.SnapshotData).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();

        builder.HasIndex(x => new { x.Sample2Id, x.Version }).IsUnique();
    }
}
