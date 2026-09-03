using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class PostBookmarkConfiguration : IEntityTypeConfiguration<PostBookmark>
{
    public void Configure(EntityTypeBuilder<PostBookmark> builder)
    {
        builder.ToTable("post_bookmarks");

        builder.HasKey(pb => pb.Id);
        builder.Property(pb => pb.Id).HasColumnName("id");

        builder.Property(pb => pb.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(pb => pb.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(pb => pb.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(pb => pb.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(pb => pb.Post)
            .WithMany(p => p.Bookmarks)
            .HasForeignKey(pb => pb.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pb => pb.User)
            .WithMany()
            .HasForeignKey(pb => pb.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one bookmark per user per post
        builder.HasIndex(pb => new { pb.PostId, pb.UserId })
            .IsUnique()
            .HasDatabaseName("uq_post_bookmarks_post_user");

        // Cursor pagination index for user's bookmarked posts
        builder.HasIndex(pb => new { pb.UserId, pb.CreatedAt, pb.Id })
            .HasDatabaseName("idx_post_bookmarks_user_created");

        builder.HasIndex(pb => pb.PostId)
            .HasDatabaseName("idx_post_bookmarks_post_id");
    }
}
