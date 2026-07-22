using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations;

public sealed class ProblemAuthoringRevisionConfiguration :
    IEntityTypeConfiguration<ProblemAuthoringRevision>
{
    public void Configure(EntityTypeBuilder<ProblemAuthoringRevision> builder)
    {
        builder.ToTable("ProblemAuthoringRevisions", table =>
        {
            table.HasCheckConstraint("CK_AuthoringRevision_Number", "\"RevisionNumber\" > 0");
            table.HasCheckConstraint("CK_AuthoringRevision_Status", "\"Status\" IN (0, 1, 2, 3)");
            table.HasCheckConstraint("CK_AuthoringRevision_TimeLimit", "\"TimeLimitMs\" > 0");
            table.HasCheckConstraint("CK_AuthoringRevision_MemoryLimit", "\"MemoryLimitKb\" > 0");
            table.HasCheckConstraint(
                "CK_AuthoringRevision_Candidate",
                "(\"Status\" IN (0, 1) AND \"CandidateSuiteSha256\" IS NULL AND \"CandidateCaseCount\" IS NULL) OR " +
                "(\"Status\" IN (2, 3) AND \"CandidateSuiteSha256\" IS NOT NULL AND \"CandidateCaseCount\" > 0)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Title).IsRequired().HasMaxLength(255);
        builder.Property(item => item.Slug).IsRequired().HasMaxLength(160);
        builder.Property(item => item.StatementMarkdown).IsRequired();
        builder.Property(item => item.ConstraintsMarkdown).IsRequired();
        builder.Property(item => item.DefinitionJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.SamplesJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.DefinitionSha256).IsRequired().HasMaxLength(64).IsFixedLength();
        builder.Property(item => item.CandidateSuiteSha256).HasMaxLength(64).IsFixedLength();
        builder.Property(item => item.CandidateToolchain).HasMaxLength(512);
        builder.Property(item => item.CandidateStatisticsJson).HasColumnType("jsonb");
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(item => new { item.ProblemId, item.RevisionNumber }).IsUnique();
        builder.HasIndex(item => new { item.OwnerUserId, item.Status, item.UpdatedAt });
        builder.HasOne(item => item.Problem).WithMany(problem => problem.AuthoringRevisions)
            .HasForeignKey(item => item.ProblemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.OwnerUser).WithMany(user => user.AuthoringRevisions)
            .HasForeignKey(item => item.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
