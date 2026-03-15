using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class TestCaseConfiguration : IEntityTypeConfiguration<TestCase>
    {
        public void Configure(EntityTypeBuilder<TestCase> builder)
        {
            builder.ToTable("TestCases");

            builder.HasKey(t => t.Id);

            builder.Property(t => t.Input).IsRequired();

            builder.Property(t => t.ExpectedOutput).IsRequired();
        }
    }
}
