using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class BlockConfiguration : IEntityTypeConfiguration<Block>
{
    public void Configure(EntityTypeBuilder<Block> builder)
    {
        builder.ToTable("blocks");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).HasColumnName("id");

        builder.Property(b => b.BlockerId).HasColumnName("blocker_id").IsRequired();
        builder.Property(b => b.BlockedId).HasColumnName("blocked_id").IsRequired();
        builder.Property(b => b.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(b => b.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(b => b.Blocker)
            .WithMany()
            .HasForeignKey(b => b.BlockerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.Blocked)
            .WithMany()
            .HasForeignKey(b => b.BlockedId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.BlockerId, b.BlockedId })
            .IsUnique()
            .HasDatabaseName("uq_blocks_blocker_blocked");

        builder.HasIndex(b => b.BlockerId)
            .HasDatabaseName("idx_blocks_blocker_id");

        builder.HasIndex(b => b.BlockedId)
            .HasDatabaseName("idx_blocks_blocked_id");

        builder.HasIndex(b => new { b.BlockerId, b.BlockedId })
            .HasDatabaseName("idx_blocks_composite");

        builder.HasIndex(b => new { b.BlockedId, b.BlockerId })
            .HasDatabaseName("idx_blocks_reverse");
    }
}
