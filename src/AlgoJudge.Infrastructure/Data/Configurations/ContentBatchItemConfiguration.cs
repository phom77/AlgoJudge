using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations;

public sealed class ContentBatchItemConfiguration : IEntityTypeConfiguration<ContentBatchItem>
{
    public void Configure(EntityTypeBuilder<ContentBatchItem> builder)
    {
        builder.ToTable("ContentBatchItems", table =>
        {
            table.HasCheckConstraint("CK_ContentBatchItem_Ordinal", "\"Ordinal\" > 0");
            table.HasCheckConstraint(
                "CK_ContentBatchItem_Action",
                "\"Action\" IN (0, 1, 2, 3)");
            table.HasCheckConstraint(
                "CK_ContentBatchItem_Status",
                "\"Status\" IN (0, 1, 2, 3, 4, 5, 6)");
            table.HasCheckConstraint(
                "CK_ContentBatchItem_Resolution",
                "(\"Status\" IN (1, 2, 3, 5) AND \"ProblemId\" IS NOT NULL AND " +
                "\"RevisionId\" IS NOT NULL) OR \"Status\" IN (0, 4, 6)");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CatalogPath).IsRequired().HasMaxLength(512);
        builder.Property(item => item.ContentHash).HasMaxLength(64).IsFixedLength();
        builder.Property(item => item.Slug).IsRequired().HasMaxLength(160);
        builder.Property(item => item.Title).IsRequired().HasMaxLength(255);
        builder.Property(item => item.StatementMarkdown).IsRequired();
        builder.Property(item => item.ConstraintsMarkdown).IsRequired();
        builder.Property(item => item.TagsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.SamplesJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.DefinitionJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.GeneratorParametersJson).IsRequired().HasColumnType("jsonb");
        builder.Property(item => item.SafeFailureCategory).HasMaxLength(64);
        builder.Property(item => item.SafeFailureMessage).HasMaxLength(1024);
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(item => new { item.BatchId, item.Ordinal }).IsUnique();
        builder.HasIndex(item => new { item.BatchId, item.Status });
        builder.HasIndex(item => new { item.Slug, item.ContentHash });
        builder.HasOne(item => item.Batch)
            .WithMany(batch => batch.Items)
            .HasForeignKey(item => item.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Problem)
            .WithMany(problem => problem.ContentBatchItems)
            .HasForeignKey(item => item.ProblemId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(item => item.Revision)
            .WithMany(revision => revision.ContentBatchItems)
            .HasForeignKey(item => item.RevisionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
