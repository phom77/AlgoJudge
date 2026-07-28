using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations;

public sealed class ContentBatchConfiguration : IEntityTypeConfiguration<ContentBatch>
{
    public void Configure(EntityTypeBuilder<ContentBatch> builder)
    {
        builder.ToTable("ContentBatches", table =>
            table.HasCheckConstraint(
                "CK_ContentBatch_Status",
                "\"Status\" IN (0, 1, 2, 3, 4, 5)"));
        builder.HasKey(item => item.Id);
        builder.Property(item => item.CatalogName).IsRequired().HasMaxLength(255);
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(item => item.UpdatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.Property(item => item.ConcurrencyToken).IsConcurrencyToken();
        builder.HasIndex(item => new { item.Status, item.UpdatedAt });
        builder.HasIndex(item => new { item.CreatedByUserId, item.CreatedAt });
        builder.HasOne(item => item.CreatedByUser)
            .WithMany(user => user.ContentBatches)
            .HasForeignKey(item => item.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
