using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("id");

        builder.Property(n => n.RecipientId).HasColumnName("recipient_id").IsRequired();
        builder.Property(n => n.ActorId).HasColumnName("actor_id").IsRequired();

        builder.Property(n => n.Type)
            .HasColumnName("type")
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<NotificationType>(v, true))
            .IsRequired();

        builder.Property(n => n.TargetId).HasColumnName("target_id");
        builder.Property(n => n.TargetType).HasColumnName("target_type").HasMaxLength(50);
        builder.Property(n => n.IsRead).HasColumnName("is_read").IsRequired().HasDefaultValue(false);
        builder.Property(n => n.ReadAt).HasColumnName("read_at");
        builder.Property(n => n.Metadata).HasColumnName("metadata").HasColumnType("jsonb").HasDefaultValue("{}");
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(n => n.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasOne(n => n.Recipient)
            .WithMany()
            .HasForeignKey(n => n.RecipientId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(n => n.Actor)
            .WithMany()
            .HasForeignKey(n => n.ActorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(n => new { n.RecipientId, n.CreatedAt, n.Id })
            .HasDatabaseName("idx_notifications_recipient_created");

        builder.HasIndex(n => n.RecipientId)
            .HasFilter("is_read = FALSE")
            .HasDatabaseName("idx_notifications_recipient_unread");

        builder.HasIndex(n => n.ActorId)
            .HasDatabaseName("idx_notifications_actor_id");

        builder.HasIndex(n => new { n.TargetType, n.TargetId })
            .HasFilter("target_id IS NOT NULL")
            .HasDatabaseName("idx_notifications_target");
    }

    private static string ToDbString(NotificationType v) => v switch
    {
        NotificationType.PostLiked => "POST_LIKED",
        NotificationType.PostReplied => "POST_REPLIED",
        NotificationType.ReplyLiked => "REPLY_LIKED",
        NotificationType.UserFollowed => "USER_FOLLOWED",
        NotificationType.UserMentioned => "USER_MENTIONED",
        _ => v.ToString().ToUpperInvariant()
    };

    private static NotificationType FromDbString(string v) => v switch
    {
        "POST_LIKED" => NotificationType.PostLiked,
        "POST_REPLIED" => NotificationType.PostReplied,
        "REPLY_LIKED" => NotificationType.ReplyLiked,
        "USER_FOLLOWED" => NotificationType.UserFollowed,
        "USER_MENTIONED" => NotificationType.UserMentioned,
        _ => Enum.Parse<NotificationType>(v, true)
    };
}
