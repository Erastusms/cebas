using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("reports");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");

        builder.Property(r => r.ReporterUserId).HasColumnName("reporter_user_id").IsRequired();
        builder.Property(r => r.TargetPostId).HasColumnName("target_post_id");
        builder.Property(r => r.TargetUserId).HasColumnName("target_user_id");

        builder.Property(r => r.Category)
            .HasColumnName("category")
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<ReportCategory>(v, true))
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(r => r.Status)
            .HasColumnName("status")
            .HasConversion(
                v => v.ToString(),
                v => Enum.Parse<ReportStatus>(v, true))
            .HasMaxLength(20)
            .HasDefaultValue(ReportStatus.PENDING)
            .IsRequired();

        builder.Property(r => r.Reason).HasColumnName("reason").HasMaxLength(1000);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");
        builder.Property(r => r.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(r => r.ResolvedByUserId).HasColumnName("resolved_by_user_id");

        // Relationships
        builder.HasOne(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.TargetPost)
            .WithMany()
            .HasForeignKey(r => r.TargetPostId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.TargetUser)
            .WithMany()
            .HasForeignKey(r => r.TargetUserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(r => r.ResolvedByUser)
            .WithMany()
            .HasForeignKey(r => r.ResolvedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Indexes
        builder.HasIndex(r => new { r.Status, r.CreatedAt })
            .HasDatabaseName("idx_reports_status_created");

        builder.HasIndex(r => new { r.Category, r.Status })
            .HasDatabaseName("idx_reports_category_status");

        builder.HasIndex(r => new { r.ReporterUserId, r.CreatedAt })
            .HasDatabaseName("idx_reports_reporter_created");

        builder.HasIndex(r => r.TargetPostId)
            .HasDatabaseName("idx_reports_target_post");

        builder.HasIndex(r => r.TargetUserId)
            .HasDatabaseName("idx_reports_target_user");
    }
}
