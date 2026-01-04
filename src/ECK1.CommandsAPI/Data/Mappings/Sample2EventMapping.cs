using ECK1.CommandsAPI.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.CommandsAPI.Data.Mappings;

public class Sample2EventMapping : IEntityTypeConfiguration<Sample2EventEntity>
{
    public void Configure(EntityTypeBuilder<Sample2EventEntity> builder)
    {
        builder.ToTable("Sample2Events");

        builder.HasKey(e => e.EventId);

        builder.Property(e => e.Sample2Id).IsRequired();
        builder.Property(e => e.EventType).HasMaxLength(256).IsRequired();
        builder.Property(e => e.EventData).HasColumnType("nvarchar(max)").IsRequired();
        builder.Property(e => e.OccurredAt).IsRequired();
        builder.Property(e => e.Version).IsRequired();

        builder.HasIndex(e => new { e.Sample2Id, e.Version }).IsUnique();
    }
}
