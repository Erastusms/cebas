using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class ModerationAuditLogConfiguration : IEntityTypeConfiguration<ModerationAuditLog>
{
    public void Configure(EntityTypeBuilder<ModerationAuditLog> builder)
    {
        builder.ToTable("moderation_audit_logs");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("id");

        builder.Property(m => m.ActorUserId).HasColumnName("actor_user_id").IsRequired();
        builder.Property(m => m.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
        builder.Property(m => m.TargetType).HasColumnName("target_type").HasMaxLength(50).IsRequired();
        builder.Property(m => m.TargetId).HasColumnName("target_id").IsRequired();
        builder.Property(m => m.Reason).HasColumnName("reason");
        builder.Property(m => m.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
        builder.Property(m => m.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Ignore(m => m.UpdatedAt);

        builder.HasOne(m => m.ActorUser)
            .WithMany()
            .HasForeignKey(m => m.ActorUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(m => new { m.ActorUserId, m.CreatedAt })
            .HasDatabaseName("idx_moderation_audit_logs_actor");

        builder.HasIndex(m => new { m.TargetType, m.TargetId })
            .HasDatabaseName("idx_moderation_audit_logs_target");

        builder.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("idx_moderation_audit_logs_created");
    }
}
