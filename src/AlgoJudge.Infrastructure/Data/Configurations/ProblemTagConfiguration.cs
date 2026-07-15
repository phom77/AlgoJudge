using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class ProblemTagConfiguration : IEntityTypeConfiguration<ProblemTag>
    {
        public void Configure(EntityTypeBuilder<ProblemTag> builder)
        {
            builder.ToTable("ProblemTags");
            builder.HasKey(problemTag => new { problemTag.ProblemId, problemTag.TagId });

            builder.HasOne(problemTag => problemTag.Problem)
                .WithMany(problem => problem.Tags)
                .HasForeignKey(problemTag => problemTag.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(problemTag => problemTag.Tag)
                .WithMany(tag => tag.Problems)
                .HasForeignKey(problemTag => problemTag.TagId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(problemTag => problemTag.TagId);
        }
    }
}
