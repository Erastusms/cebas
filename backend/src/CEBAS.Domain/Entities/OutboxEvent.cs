using CEBAS.Domain.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Domain.Entities;

/// <summary>
/// Domain entity representing a durable event staged for asynchronous publication via the Transactional Outbox Pattern.
/// </summary>
public class OutboxEvent : Entity
{
    public string EventType { get; private set; } = null!;
    public string AggregateType { get; private set; } = null!;
    public Guid AggregateId { get; private set; }
    public string Payload { get; private set; } = null!;
    public OutboxEventStatus Status { get; private set; } = OutboxEventStatus.Pending;
    public int AttemptCount { get; private set; }
    public int MaxRetries { get; private set; } = 5;
    public DateTimeOffset NextAttemptAt { get; private set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? CausationId { get; private set; }

    // Parameterless constructor for EF Core
    protected OutboxEvent() { }

    public static OutboxEvent Create(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        string payload,
        int maxRetries = 5,
        string? correlationId = null,
        string? causationId = null)
    {
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new ValidationException("EventType", "Event type is required.");
        }

        if (string.IsNullOrWhiteSpace(aggregateType))
        {
            throw new ValidationException("AggregateType", "Aggregate type is required.");
        }

        if (aggregateId == Guid.Empty)
        {
            throw new ValidationException("AggregateId", "Aggregate ID is required.");
        }

        if (string.IsNullOrWhiteSpace(payload))
        {
            throw new ValidationException("Payload", "Payload is required.");
        }

        var now = DateTimeOffset.UtcNow;
        return new OutboxEvent
        {
            Id = Uuid7.New(),
            EventType = eventType,
            AggregateType = aggregateType,
            AggregateId = aggregateId,
            Payload = payload,
            Status = OutboxEventStatus.Pending,
            AttemptCount = 0,
            MaxRetries = maxRetries > 0 ? maxRetries : 5,
            NextAttemptAt = now,
            ProcessedAt = null,
            ErrorMessage = null,
            CorrelationId = correlationId,
            CausationId = causationId,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void MarkProcessing(DateTimeOffset now)
    {
        Status = OutboxEventStatus.Processing;
        AttemptCount++;
        ProcessedAt = now;
        UpdatedAt = now;
    }

    public void MarkPublished(DateTimeOffset now)
    {
        Status = OutboxEventStatus.Published;
        ProcessedAt = now;
        ErrorMessage = null;
        UpdatedAt = now;
    }

    public void MarkFailed(string errorMessage, TimeSpan? retryBackoff, DateTimeOffset now)
    {
        ErrorMessage = errorMessage;
        UpdatedAt = now;

        if (AttemptCount >= MaxRetries || retryBackoff == null)
        {
            Status = OutboxEventStatus.Failed;
        }
        else
        {
            Status = OutboxEventStatus.Pending;
            NextAttemptAt = now.Add(retryBackoff.Value);
        }
    }
}
