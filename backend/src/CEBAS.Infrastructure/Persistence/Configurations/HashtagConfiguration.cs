using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class HashtagConfiguration : IEntityTypeConfiguration<Hashtag>
{
    public void Configure(EntityTypeBuilder<Hashtag> builder)
    {
        builder.ToTable("hashtags");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("id");

        builder.Property(h => h.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(Hashtag.MaxNameLength)
            .IsRequired();

        builder.Property(h => h.DisplayName)
            .HasColumnName("display_name")
            .HasMaxLength(Hashtag.MaxNameLength)
            .IsRequired();

        builder.Property(h => h.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(h => h.UpdatedAt)
            .HasColumnName("updated_at");

        builder.HasIndex(h => h.NormalizedName)
            .IsUnique()
            .HasDatabaseName("uq_hashtags_normalized_name");

        builder.HasMany(h => h.PostHashtags)
            .WithOne(ph => ph.Hashtag)
            .HasForeignKey(ph => ph.HashtagId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
