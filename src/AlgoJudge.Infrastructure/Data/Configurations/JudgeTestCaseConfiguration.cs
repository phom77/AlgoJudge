using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class JudgeTestCaseConfiguration : IEntityTypeConfiguration<JudgeTestCase>
    {
        public void Configure(EntityTypeBuilder<JudgeTestCase> builder)
        {
            builder.ToTable("JudgeTestCases", table =>
                table.HasCheckConstraint("CK_JudgeTestCase_Ordinal", "\"Ordinal\" > 0"));

            builder.HasKey(testCase => testCase.Id);
            builder.Property(testCase => testCase.Input).IsRequired();
            builder.Property(testCase => testCase.ExpectedOutput).IsRequired();

            builder.HasOne(testCase => testCase.Problem)
                .WithMany(problem => problem.JudgeTestCases)
                .HasForeignKey(testCase => testCase.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(testCase => new { testCase.ProblemId, testCase.Ordinal })
                .IsUnique();
        }
    }
}
