using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class FollowConfiguration : IEntityTypeConfiguration<Follow>
{
    public void Configure(EntityTypeBuilder<Follow> builder)
    {
        builder.ToTable("follows");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("id");

        builder.Property(f => f.FollowerId).HasColumnName("follower_id").IsRequired();
        builder.Property(f => f.FollowingId).HasColumnName("following_id").IsRequired();
        builder.Property(f => f.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(f => f.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(f => f.Follower)
            .WithMany()
            .HasForeignKey(f => f.FollowerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(f => f.Following)
            .WithMany()
            .HasForeignKey(f => f.FollowingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(f => new { f.FollowerId, f.FollowingId })
            .IsUnique()
            .HasDatabaseName("uq_follows_follower_following");

        builder.HasIndex(f => new { f.FollowingId, f.CreatedAt, f.Id })
            .HasDatabaseName("idx_follows_following_created");

        builder.HasIndex(f => new { f.FollowerId, f.CreatedAt, f.Id })
            .HasDatabaseName("idx_follows_follower_created");

        builder.HasIndex(f => f.FollowerId)
            .HasDatabaseName("idx_follows_follower_id");

        builder.HasIndex(f => f.FollowingId)
            .HasDatabaseName("idx_follows_following_id");
    }
}
