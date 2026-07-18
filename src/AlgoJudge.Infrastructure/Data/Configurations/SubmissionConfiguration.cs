using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
    {
        public void Configure(EntityTypeBuilder<Submission> builder)
        {
            builder.ToTable("Submissions", table =>
            {
                table.HasCheckConstraint(
                    "CK_Submission_AttemptCount",
                    "\"AttemptCount\" >= 0");
                table.HasCheckConstraint(
                    "CK_Submission_SystemTestSuiteVersion",
                    "\"SystemTestSuiteVersion\" > 0");
                table.HasCheckConstraint(
                    "CK_Submission_RunningClaim",
                    "\"Status\" <> 2 OR (\"WorkerId\" IS NOT NULL AND " +
                    "\"ClaimToken\" IS NOT NULL AND \"LeaseExpiresAt\" IS NOT NULL)");
            });

            builder.HasKey(s => s.Id);

            builder.Property(s => s.SourceCode).IsRequired();

            builder.Property(s => s.OriginalFileName).HasMaxLength(255);

            builder.Property(s => s.Language)
                   .IsRequired()
                   .HasMaxLength(50);

            builder.Property(s => s.WorkerId)
                   .HasMaxLength(128);

            builder.Property(s => s.AttemptCount)
                   .HasDefaultValue(0);

            builder.Property(s => s.SystemTestSuiteVersion)
                   .HasDefaultValue(1);

            builder.HasOne(s => s.User)
                   .WithMany(u => u.Submissions)
                   .HasForeignKey(s => s.UserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(s => s.Problem)
                   .WithMany(p => p.Submissions)
                   .HasForeignKey(s => s.ProblemId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => s.UserId);

            builder.HasIndex(s => s.ProblemId);

            builder.HasIndex(s => s.CreatedAt).IsDescending();

            builder.HasIndex(s => new { s.Status, s.CreatedAt, s.Id });

            builder.HasIndex(s => new { s.Status, s.LeaseExpiresAt });
            
            builder.HasIndex(s => new { s.UserId, s.ProblemId });

            builder.HasIndex(s => new { s.UserId, s.Status, s.ProblemId });
        }
    }
}
