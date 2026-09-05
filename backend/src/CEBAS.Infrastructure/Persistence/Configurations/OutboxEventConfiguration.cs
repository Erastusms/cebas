using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using CEBAS.Domain.Entities;

namespace CEBAS.Infrastructure.Persistence.Configurations;

public class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");

        builder.Property(e => e.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.AggregateType).HasColumnName("aggregate_type").HasMaxLength(100).IsRequired();
        builder.Property(e => e.AggregateId).HasColumnName("aggregate_id").IsRequired();
        builder.Property(e => e.Payload).HasColumnName("payload").HasColumnType("jsonb").IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasMaxLength(20)
            .HasConversion(
                v => v.ToString().ToUpperInvariant(),
                v => Enum.Parse<OutboxEventStatus>(v, true)
            )
            .IsRequired()
            .HasDefaultValue(OutboxEventStatus.Pending);

        builder.Property(e => e.AttemptCount).HasColumnName("attempt_count").IsRequired().HasDefaultValue(0);
        builder.Property(e => e.MaxRetries).HasColumnName("max_retries").IsRequired().HasDefaultValue(5);
        builder.Property(e => e.NextAttemptAt).HasColumnName("next_attempt_at").IsRequired();
        builder.Property(e => e.ProcessedAt).HasColumnName("processed_at");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message");
        builder.Property(e => e.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
        builder.Property(e => e.CausationId).HasColumnName("causation_id").HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at").IsRequired();

        builder.HasIndex(e => new { e.NextAttemptAt, e.CreatedAt })
            .HasFilter("status IN ('PENDING', 'PROCESSING')")
            .HasDatabaseName("idx_outbox_events_polling");

        builder.HasIndex(e => new { e.Status, e.CreatedAt })
            .HasDatabaseName("idx_outbox_events_status_created");

        builder.HasIndex(e => new { e.AggregateType, e.AggregateId })
            .HasDatabaseName("idx_outbox_events_aggregate");

        builder.HasIndex(e => e.CorrelationId)
            .HasFilter("correlation_id IS NOT NULL")
            .HasDatabaseName("idx_outbox_events_correlation");
    }
}
