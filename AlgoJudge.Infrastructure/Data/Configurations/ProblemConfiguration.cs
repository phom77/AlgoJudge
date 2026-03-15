using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class ProblemConfiguration
    {
        public class ProblmConfiguration : IEntityTypeConfiguration<Problem>
        {
            public void Configure(EntityTypeBuilder<Problem> builder)
            {
                builder.ToTable("Problems");

                builder.HasKey(p => p.Id);

                builder.Property(p => p.Title)
                       .IsRequired()
                       .HasMaxLength(255);

                builder.Property(p => p.Description)
                       .IsRequired();

                builder.HasMany(p => p.TestCases)
                       .WithOne(t => t.Problem)
                       .HasForeignKey(t => t.ProblemId)
                       .OnDelete(DeleteBehavior.Cascade);
            }
        }

    }
}
