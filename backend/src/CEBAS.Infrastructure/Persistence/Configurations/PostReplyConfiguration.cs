using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class PostReplyConfiguration : IEntityTypeConfiguration<PostReply>
{
    public void Configure(EntityTypeBuilder<PostReply> builder)
    {
        builder.ToTable("post_replies");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(r => r.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(r => r.ParentReplyId).HasColumnName("parent_reply_id");
        builder.Property(r => r.Content).HasColumnName("content").HasMaxLength(1000).IsRequired();
        builder.Property(r => r.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false).IsRequired();
        builder.Property(r => r.DeletedAt).HasColumnName("deleted_at");
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(r => r.Post)
            .WithMany(p => p.Replies)
            .HasForeignKey(r => r.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Author)
            .WithMany()
            .HasForeignKey(r => r.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.ParentReply)
            .WithMany(pr => pr.ChildReplies)
            .HasForeignKey(r => r.ParentReplyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => new { r.PostId, r.CreatedAt, r.Id })
            .HasDatabaseName("idx_post_replies_post_created");

        builder.HasIndex(r => new { r.ParentReplyId, r.CreatedAt, r.Id })
            .HasDatabaseName("idx_post_replies_parent_created");

        builder.HasIndex(r => new { r.PostId, r.ParentReplyId, r.CreatedAt, r.Id })
            .HasDatabaseName("idx_post_replies_post_parent");

        builder.HasIndex(r => r.AuthorId)
            .HasDatabaseName("idx_post_replies_author_id");

        builder.HasIndex(r => r.IsDeleted)
            .HasDatabaseName("idx_post_replies_is_deleted");
    }
}
