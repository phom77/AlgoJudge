using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations;

public sealed class CodeRunConfiguration : IEntityTypeConfiguration<CodeRun>
{
    public void Configure(EntityTypeBuilder<CodeRun> builder)
    {
        builder.ToTable("CodeRuns", table =>
        {
            table.HasCheckConstraint("CK_CodeRun_AttemptCount", "\"AttemptCount\" >= 0");
            table.HasCheckConstraint(
                "CK_CodeRun_RunningClaim",
                "\"Status\" <> 2 OR (\"WorkerId\" IS NOT NULL AND \"ClaimToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL)");
        });
        builder.HasKey(run => run.Id);
        builder.Property(run => run.SourceCode).IsRequired();
        builder.Property(run => run.Input).IsRequired();
        builder.Property(run => run.Language).IsRequired().HasMaxLength(50);
        builder.Property(run => run.WorkerId).HasMaxLength(128);
        builder.Property(run => run.AttemptCount).HasDefaultValue(0);
        builder.HasOne(run => run.User).WithMany(user => user.CodeRuns)
            .HasForeignKey(run => run.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(run => run.Problem).WithMany(problem => problem.CodeRuns)
            .HasForeignKey(run => run.ProblemId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(run => run.UserId);
        builder.HasIndex(run => run.ProblemId);
        builder.HasIndex(run => run.CreatedAt).IsDescending();
        builder.HasIndex(run => new { run.Status, run.CreatedAt, run.Id });
        builder.HasIndex(run => new { run.Status, run.LeaseExpiresAt });
    }
}
