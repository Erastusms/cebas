using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class PostLikeConfiguration : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> builder)
    {
        builder.ToTable("post_likes");

        builder.HasKey(pl => pl.Id);
        builder.Property(pl => pl.Id).HasColumnName("id");

        builder.Property(pl => pl.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(pl => pl.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(pl => pl.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(pl => pl.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(pl => pl.Post)
            .WithMany(p => p.Likes)
            .HasForeignKey(pl => pl.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pl => pl.User)
            .WithMany()
            .HasForeignKey(pl => pl.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Unique constraint: one like per user per post
        builder.HasIndex(pl => new { pl.PostId, pl.UserId })
            .IsUnique()
            .HasDatabaseName("uq_post_likes_post_user");

        // Cursor pagination index for user's liked posts
        builder.HasIndex(pl => new { pl.UserId, pl.CreatedAt, pl.Id })
            .HasDatabaseName("idx_post_likes_user_created");

        builder.HasIndex(pl => pl.PostId)
            .HasDatabaseName("idx_post_likes_post_id");
    }
}
