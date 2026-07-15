using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class ProblemSampleConfiguration : IEntityTypeConfiguration<ProblemSample>
    {
        public void Configure(EntityTypeBuilder<ProblemSample> builder)
        {
            builder.ToTable("ProblemSamples", table =>
                table.HasCheckConstraint("CK_ProblemSample_Ordinal", "\"Ordinal\" > 0"));

            builder.HasKey(sample => sample.Id);
            builder.Property(sample => sample.Input).IsRequired();
            builder.Property(sample => sample.ExpectedOutput).IsRequired();

            builder.HasOne(sample => sample.Problem)
                .WithMany(problem => problem.Samples)
                .HasForeignKey(sample => sample.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(sample => new { sample.ProblemId, sample.Ordinal })
                .IsUnique();
        }
    }
}
