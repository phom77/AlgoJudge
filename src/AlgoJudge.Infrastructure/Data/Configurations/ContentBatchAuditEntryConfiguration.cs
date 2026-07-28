using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations;

public sealed class ContentBatchAuditEntryConfiguration :
    IEntityTypeConfiguration<ContentBatchAuditEntry>
{
    public void Configure(EntityTypeBuilder<ContentBatchAuditEntry> builder)
    {
        builder.ToTable("ContentBatchAuditEntries");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Action).IsRequired().HasMaxLength(64);
        builder.Property(item => item.Result).IsRequired().HasMaxLength(32);
        builder.Property(item => item.SafeFailureCategory).HasMaxLength(64);
        builder.Property(item => item.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        builder.HasIndex(item => new { item.BatchId, item.CreatedAt });
        builder.HasIndex(item => new { item.AdminUserId, item.CreatedAt });
        builder.HasOne(item => item.Batch)
            .WithMany(batch => batch.AuditEntries)
            .HasForeignKey(item => item.BatchId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.Item)
            .WithMany(batchItem => batchItem.AuditEntries)
            .HasForeignKey(item => item.ItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(item => item.AdminUser)
            .WithMany(user => user.ContentBatchAuditEntries)
            .HasForeignKey(item => item.AdminUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
