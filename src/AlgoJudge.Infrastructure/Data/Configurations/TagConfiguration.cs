using AlgoJudge.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AlgoJudge.Infrastructure.Data.Configurations
{
    public class TagConfiguration : IEntityTypeConfiguration<Tag>
    {
        public void Configure(EntityTypeBuilder<Tag> builder)
        {
            builder.ToTable("Tags");
            builder.HasKey(tag => tag.Id);

            builder.Property(tag => tag.Slug)
                .IsRequired()
                .HasMaxLength(80);

            builder.Property(tag => tag.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.HasIndex(tag => tag.Slug).IsUnique();
        }
    }
}
