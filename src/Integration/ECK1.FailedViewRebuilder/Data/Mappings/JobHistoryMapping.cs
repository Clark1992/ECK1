using ECK1.FailedViewRebuilder.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ECK1.FailedViewRebuilder.Data.Mappings;

public class JobHistoryConfiguration : IEntityTypeConfiguration<JobHistory>
{
    public void Configure(EntityTypeBuilder<JobHistory> builder)
    {
        builder.ToTable("JobHistory", "dbo");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.Id)
               .IsRequired();

        builder.Property(j => j.Name)
               .IsRequired()
               .HasMaxLength(512);

        builder.Property(j => j.StartedAt)
               .IsRequired();

        builder.Property(j => j.FinishedAt)
               .IsRequired(false);

        builder.Property(j => j.IsSuccess)
               .IsRequired(false);

        builder.Property(j => j.ErrorMessage)
               .IsRequired(false)
               .HasMaxLength(4000);
    }
}
