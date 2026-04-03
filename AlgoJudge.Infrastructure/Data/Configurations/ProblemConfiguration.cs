using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class ProblemConfiguration : IEntityTypeConfiguration<Problem>
    {
        public void Configure(EntityTypeBuilder<Problem> builder)
        {
            builder.ToTable("Problems", table =>
            {
                table.HasCheckConstraint("CK_Problem_TimeLimit", "\"TimeLimit\" > 0");
                table.HasCheckConstraint("CK_Problem_MemoryLimit", "\"MemoryLimit\" > 0");
                table.HasCheckConstraint("CK_Problem_Score", "\"Score\" > 0");
            });

            builder.HasKey(p => p.Id);

            builder.Property(p => p.Title)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(p => p.Description)
                   .IsRequired();

            builder.HasOne(p => p.Creator)
                   .WithMany() 
                   .HasForeignKey(p => p.CreatedBy)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasMany(p => p.TestCases)
                   .WithOne(t => t.Problem)
                   .HasForeignKey(t => t.ProblemId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}