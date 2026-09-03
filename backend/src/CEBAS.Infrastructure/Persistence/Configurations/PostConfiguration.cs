using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class PostConfiguration : IEntityTypeConfiguration<Post>
{
    public void Configure(EntityTypeBuilder<Post> builder)
    {
        builder.ToTable("posts");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("id");

        builder.Property(p => p.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(p => p.Content).HasColumnName("content").HasMaxLength(1000).IsRequired();
        builder.Property(p => p.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(p => p.DeletedAt).HasColumnName("deleted_at");
        builder.Property(p => p.ReplyCount).HasColumnName("reply_count").HasDefaultValue(0).IsRequired();
        builder.Property(p => p.MediaCount).HasColumnName("media_count").HasDefaultValue(0).IsRequired();
        builder.Property(p => p.LikeCount).HasColumnName("like_count").HasDefaultValue(0).IsRequired();
        builder.Property(p => p.BookmarkCount).HasColumnName("bookmark_count").HasDefaultValue(0).IsRequired();
        builder.Property(p => p.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(p => p.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(p => p.Author)
            .WithMany()
            .HasForeignKey(p => p.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.MediaAttachments)
            .WithOne(pm => pm.Post)
            .HasForeignKey(pm => pm.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Replies)
            .WithOne(r => r.Post)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(p => new { p.AuthorId, p.CreatedAt, p.Id })
            .HasDatabaseName("idx_posts_author_created");

        builder.HasIndex(p => new { p.CreatedAt, p.Id })
            .HasDatabaseName("idx_posts_created_pagination");

        builder.HasIndex(p => new { p.CreatedAt, p.Id })
            .HasDatabaseName("idx_posts_created_at");

        builder.HasIndex(p => p.IsDeleted)
            .HasDatabaseName("idx_posts_is_deleted");

        builder.HasIndex(p => p.AuthorId)
            .HasDatabaseName("idx_posts_author_id");
    }
}
