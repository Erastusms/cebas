using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class PostHashtagConfiguration : IEntityTypeConfiguration<PostHashtag>
{
    public void Configure(EntityTypeBuilder<PostHashtag> builder)
    {
        builder.ToTable("post_hashtags");

        builder.HasKey(ph => ph.Id);
        builder.Property(ph => ph.Id).HasColumnName("id");

        builder.Property(ph => ph.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(ph => ph.HashtagId).HasColumnName("hashtag_id").IsRequired();
        builder.Property(ph => ph.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(ph => ph.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(ph => ph.Post)
            .WithMany()
            .HasForeignKey(ph => ph.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(ph => ph.Hashtag)
            .WithMany(h => h.PostHashtags)
            .HasForeignKey(ph => ph.HashtagId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(ph => new { ph.PostId, ph.HashtagId })
            .IsUnique()
            .HasDatabaseName("uq_post_hashtags_post_hashtag");

        builder.HasIndex(ph => new { ph.HashtagId, ph.CreatedAt, ph.PostId })
            .HasDatabaseName("idx_post_hashtags_hashtag_created");

        builder.HasIndex(ph => ph.PostId)
            .HasDatabaseName("idx_post_hashtags_post_id");
    }
}
