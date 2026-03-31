using ECK1.FailedViewRebuilder.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.FailedViewRebuilder.Data.Mappings;

public class EventFailureMapping : IEntityTypeConfiguration<EventFailure>
{
    public void Configure(EntityTypeBuilder<EventFailure> builder)
    {
        builder.ToTable("EventFailures", "dbo");

        builder.HasKey(x => new { x.EntityType, x.EntityId });

        builder.Property(x => x.EntityType)
            .HasColumnType("nvarchar(64)")
            .IsRequired();

        builder.Property(x => x.EntityId)
            .IsRequired();

        builder.Property(x => x.FailedEventType)
            .HasColumnType("nvarchar(512)")
            .IsRequired();

        builder.Property(x => x.FailureOccurredAt)
            .IsRequired();

        builder.Property(x => x.ErrorMessage)
            .HasColumnType("nvarchar(4000)");

        builder.Property(x => x.StackTrace)
            .HasColumnType("nvarchar(4000)")
            .IsRequired();

        builder.HasIndex(x => x.FailureOccurredAt)
            .HasDatabaseName("IX_EventFailures_FailureOccurredAt");
    }
}
