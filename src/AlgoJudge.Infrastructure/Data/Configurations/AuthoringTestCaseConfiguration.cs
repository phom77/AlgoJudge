using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations;

public sealed class AuthoringTestCaseConfiguration : IEntityTypeConfiguration<AuthoringTestCase>
{
    public void Configure(EntityTypeBuilder<AuthoringTestCase> builder)
    {
        builder.ToTable("AuthoringTestCases", table =>
        {
            table.HasCheckConstraint("CK_AuthoringTestCase_Ordinal", "\"Ordinal\" > 0");
            table.HasCheckConstraint(
                "CK_AuthoringTestCase_Group",
                "\"Group\" IN ('handwritten', 'edge', 'random', 'adversarial', 'stress')");
        });
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Name).IsRequired().HasMaxLength(160);
        builder.Property(item => item.Group).IsRequired().HasMaxLength(32);
        builder.Property(item => item.Input).IsRequired();
        builder.Property(item => item.ExpectedOutput).IsRequired();
        builder.Property(item => item.KilledWrongSolutionsJson).IsRequired().HasColumnType("jsonb");
        builder.HasIndex(item => new { item.RevisionId, item.Ordinal }).IsUnique();
        builder.HasIndex(item => new { item.RevisionId, item.Name }).IsUnique();
        builder.HasOne(item => item.Revision).WithMany(revision => revision.CandidateTestCases)
            .HasForeignKey(item => item.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}
