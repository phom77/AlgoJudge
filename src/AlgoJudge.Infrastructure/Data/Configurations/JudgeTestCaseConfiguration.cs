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
            {
                table.HasCheckConstraint("CK_JudgeTestCase_Ordinal", "\"Ordinal\" > 0");
                table.HasCheckConstraint("CK_JudgeTestCase_SystemTestSuiteVersion", "\"SystemTestSuiteVersion\" > 0");
            });

            builder.HasKey(testCase => testCase.Id);
            builder.Property(testCase => testCase.Input).IsRequired();
            builder.Property(testCase => testCase.ExpectedOutput).IsRequired();

            builder.HasOne(testCase => testCase.Problem)
                .WithMany(problem => problem.JudgeTestCases)
                .HasForeignKey(testCase => testCase.ProblemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Property(testCase => testCase.SystemTestSuiteVersion).HasDefaultValue(1);

            builder.HasIndex(testCase => new
                { testCase.ProblemId, testCase.SystemTestSuiteVersion, testCase.Ordinal })
                .IsUnique();
        }
    }
}
