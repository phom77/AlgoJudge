using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations;

public sealed class ContentGenerationJobConfiguration : IEntityTypeConfiguration<ContentGenerationJob>
{
    public void Configure(EntityTypeBuilder<ContentGenerationJob> builder)
    {
        builder.ToTable("ContentGenerationJobs", table =>
        {
            table.HasCheckConstraint("CK_ContentGenerationJob_Status", "\"Status\" IN (0, 1, 2, 3)");
            table.HasCheckConstraint("CK_ContentGenerationJob_Attempts", "\"AttemptCount\" >= 0");
            table.HasCheckConstraint(
                "CK_ContentGenerationJob_Claim",
                "(\"Status\" = 1 AND \"WorkerId\" IS NOT NULL AND \"ClaimToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL) OR " +
                "(\"Status\" <> 1 AND \"WorkerId\" IS NULL AND \"ClaimToken\" IS NULL AND \"LeaseExpiresAt\" IS NULL)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.DefinitionSnapshotJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.DefinitionSha256).IsRequired().HasMaxLength(64).IsFixedLength();
        builder.Property(item => item.WorkerId).HasMaxLength(128);
        builder.Property(item => item.ErrorCode).HasMaxLength(64);
        builder.Property(item => item.ErrorMessage).HasMaxLength(1024);
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(item => new { item.Status, item.CreatedAt });
        builder.HasIndex(item => item.LeaseExpiresAt);
        builder.HasIndex(item => item.RevisionId)
            .HasFilter("\"Status\" IN (0, 1)")
            .IsUnique();
        builder.HasOne(item => item.Revision).WithMany(revision => revision.GenerationJobs)
            .HasForeignKey(item => item.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}
