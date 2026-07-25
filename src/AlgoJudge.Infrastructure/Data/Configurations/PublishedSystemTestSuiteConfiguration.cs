using AlgoJudge.Domain.Entities;
using AlgoJudge.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations;

public sealed class PublishedSystemTestSuiteConfiguration : IEntityTypeConfiguration<PublishedSystemTestSuite>
{
    public void Configure(EntityTypeBuilder<PublishedSystemTestSuite> builder)
    {
        builder.ToTable("SystemTestSuites", table =>
        {
            table.HasCheckConstraint("CK_SystemTestSuite_Version", "\"Version\" > 0");
            table.HasCheckConstraint(
                "CK_SystemTestSuite_OutputChecker",
                "\"OutputCheckerKind\" IN (0, 1, 2)");
            table.HasCheckConstraint(
                "CK_SystemTestSuite_OutputCheckerTolerance",
                "(\"OutputCheckerKind\" IN (0, 1) AND \"AbsoluteTolerance\" IS NULL AND \"RelativeTolerance\" IS NULL) OR " +
                "(\"OutputCheckerKind\" = 2 AND \"AbsoluteTolerance\" IS NOT NULL AND \"RelativeTolerance\" IS NOT NULL AND " +
                "\"AbsoluteTolerance\" NOT IN ('Infinity'::double precision, '-Infinity'::double precision, 'NaN'::double precision) AND " +
                "\"RelativeTolerance\" NOT IN ('Infinity'::double precision, '-Infinity'::double precision, 'NaN'::double precision) AND \"AbsoluteTolerance\" >= 0 AND " +
                "\"RelativeTolerance\" >= 0 AND (\"AbsoluteTolerance\" > 0 OR \"RelativeTolerance\" > 0))");
        });

        builder.HasKey(suite => new { suite.ProblemId, suite.Version });
        builder.Property(suite => suite.OutputCheckerKind)
            .HasDefaultValue(OutputCheckerKind.TokenExact);
        builder.Property(suite => suite.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP");

        builder.HasOne(suite => suite.Problem)
            .WithMany(problem => problem.SystemTestSuites)
            .HasForeignKey(suite => suite.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
