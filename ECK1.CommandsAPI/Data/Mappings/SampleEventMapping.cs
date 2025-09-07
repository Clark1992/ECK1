using ECK1.CommandsAPI.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.CommandsAPI.Data.Mappings;

public class SampleEventMapping: IEntityTypeConfiguration<SampleEventEntity>
{
    public void Configure(EntityTypeBuilder<SampleEventEntity> builder)
    {
        builder.ToTable("SampleEvents");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.SampleId).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(256).IsRequired();
        builder.Property(e => e.EventData).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Version).IsRequired();

        builder.HasIndex(e => new { e.SampleId, e.Version }).IsUnique();

    }
}
