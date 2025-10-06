using ECK1.FailedViewRebuilder.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.FailedViewRebuilder.Data.Mappings;

public class SampleEventFailureMapping : IEntityTypeConfiguration<SampleEventFailure>
{
    public void Configure(EntityTypeBuilder<SampleEventFailure> builder)
    {
        builder.ToTable("SampleEventFailures");

        builder.HasKey(x => x.SampleId);
        builder.Property(x => x.StackTrace).HasColumnType("nvarchar(4000)").IsRequired();
        builder.Property(x => x.FailureOccurredAt).IsRequired();
        builder.Property(x => x.ErrorMessage).HasColumnType("nvarchar(4000)");
        builder.Property(x => x.FailedEventType).HasColumnType("nvarchar(512)");
    }
}