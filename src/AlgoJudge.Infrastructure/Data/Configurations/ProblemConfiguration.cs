using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class ProblemConfiguration : IEntityTypeConfiguration<Problem>
    {
        public void Configure(EntityTypeBuilder<Problem> builder)
        {
            builder.ToTable("Problems", table =>
            {
                table.HasCheckConstraint("CK_Problem_TimeLimitMs", "\"TimeLimitMs\" > 0");
                table.HasCheckConstraint("CK_Problem_MemoryLimitKb", "\"MemoryLimitKb\" > 0");
                table.HasCheckConstraint("CK_Problem_JudgeVersion", "\"JudgeVersion\" > 0");
                table.HasCheckConstraint(
                    "CK_Problem_ExecutionMode",
                    "\"ExecutionMode\" IN (0, 1)");
                table.HasCheckConstraint(
                    "CK_Problem_FunctionConfiguration",
                    "(\"ExecutionMode\" = 0 AND \"FunctionSignatureJson\" IS NULL AND " +
                    "\"FunctionAdapterTemplate\" IS NULL) OR " +
                    "(\"ExecutionMode\" = 1 AND \"FunctionSignatureJson\" IS NOT NULL AND " +
                    "\"FunctionAdapterTemplate\" IS NOT NULL)");
            });

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Slug)
                   .IsRequired()
                   .HasMaxLength(160);

            builder.Property(p => p.Title)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(p => p.StatementMarkdown)
                   .IsRequired();

            builder.Property(p => p.ConstraintsMarkdown)
                   .IsRequired();

            builder.Property(p => p.Status)
                   .HasDefaultValue(Domain.Enums.ProblemStatus.Draft);

            builder.Property(p => p.ExecutionMode)
                   .HasDefaultValue(Domain.Enums.ProblemExecutionMode.StdinStdout);

            builder.Property(p => p.FunctionSignatureJson)
                   .HasColumnType("jsonb");

            builder.Property(p => p.JudgeVersion)
                   .HasDefaultValue(1);

            builder.Property(p => p.CreatedAt)
                   .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.Property(p => p.UpdatedAt)
                   .HasDefaultValueSql("CURRENT_TIMESTAMP");

            builder.HasIndex(p => p.Slug)
                   .IsUnique();

            builder.HasIndex(p => new { p.Status, p.CreatedAt });
        }
    }
}
