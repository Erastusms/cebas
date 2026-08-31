using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class PostMediaConfiguration : IEntityTypeConfiguration<PostMedia>
{
    public void Configure(EntityTypeBuilder<PostMedia> builder)
    {
        builder.ToTable("post_media");

        builder.HasKey(pm => pm.Id);
        builder.Property(pm => pm.Id).HasColumnName("id");

        builder.Property(pm => pm.PostId).HasColumnName("post_id").IsRequired();
        builder.Property(pm => pm.MediaId).HasColumnName("media_id").IsRequired();
        builder.Property(pm => pm.Position).HasColumnName("position").IsRequired();
        builder.Property(pm => pm.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(pm => pm.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(pm => pm.Post)
            .WithMany(p => p.MediaAttachments)
            .HasForeignKey(pm => pm.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(pm => pm.Media)
            .WithMany()
            .HasForeignKey(pm => pm.MediaId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(pm => new { pm.PostId, pm.Position })
            .IsUnique()
            .HasDatabaseName("uq_post_media_post_position");

        builder.HasIndex(pm => new { pm.PostId, pm.MediaId })
            .IsUnique()
            .HasDatabaseName("uq_post_media_post_media");

        builder.HasIndex(pm => new { pm.PostId, pm.Position })
            .HasDatabaseName("idx_post_media_post_id");

        builder.HasIndex(pm => pm.MediaId)
            .HasDatabaseName("idx_post_media_media_id");
    }
}
