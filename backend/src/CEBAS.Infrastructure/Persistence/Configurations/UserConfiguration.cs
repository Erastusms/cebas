using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("id");

        builder.Property(u => u.Username).HasColumnName("username").HasMaxLength(30).IsRequired();
        builder.Property(u => u.Email).HasColumnName("email").HasMaxLength(255).IsRequired();
        builder.Property(u => u.PasswordHash).HasColumnName("password_hash").HasMaxLength(255).IsRequired();
        builder.Property(u => u.DisplayName).HasColumnName("display_name").HasMaxLength(50).IsRequired();
        builder.Property(u => u.Bio).HasColumnName("bio").HasMaxLength(160);
        builder.Property(u => u.AvatarUrl).HasColumnName("avatar_url").HasMaxLength(500);
        builder.Property(u => u.AvatarMediaId).HasColumnName("avatar_media_id");
        builder.Property(u => u.BannerUrl).HasColumnName("banner_url").HasMaxLength(500);
        builder.Property(u => u.BannerMediaId).HasColumnName("banner_media_id");

        builder.HasOne(u => u.AvatarMedia)
            .WithMany()
            .HasForeignKey(u => u.AvatarMediaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(u => u.BannerMedia)
            .WithMany()
            .HasForeignKey(u => u.BannerMediaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Property(u => u.Role)
            .HasColumnName("role")
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<UserRole>(v, true))
            .IsRequired();

        builder.Property(u => u.IsVerified).HasColumnName("is_verified").IsRequired().HasDefaultValue(false);
        builder.Property(u => u.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Username).HasDatabaseName("idx_users_username_lower");
        builder.HasIndex(u => u.Email).HasDatabaseName("idx_users_email_lower");
    }
}
