using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class MediaConfiguration : IEntityTypeConfiguration<Media>
{
    public void Configure(EntityTypeBuilder<Media> builder)
    {
        builder.ToTable("media");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.OwnerUserId).HasColumnName("owner_user_id").IsRequired();
        builder.Property(m => m.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(m => m.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        builder.Property(m => m.MimeType).HasColumnName("mime_type").HasMaxLength(100).IsRequired();
        builder.Property(m => m.FileSize).HasColumnName("file_size").IsRequired();

        builder.Property(m => m.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<MediaStatus>(v, true))
            .IsRequired();

        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");
        builder.Property(m => m.ConfirmedAt).HasColumnName("confirmed_at");
        builder.Property(m => m.DeletedAt).HasColumnName("deleted_at");

        builder.HasOne(m => m.OwnerUser)
            .WithMany()
            .HasForeignKey(m => m.OwnerUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => m.OwnerUserId).HasDatabaseName("idx_media_owner_user_id");
        builder.HasIndex(m => m.Status).HasDatabaseName("idx_media_status");
        builder.HasIndex(m => m.CreatedAt).HasDatabaseName("idx_media_created_at");
    }
}
