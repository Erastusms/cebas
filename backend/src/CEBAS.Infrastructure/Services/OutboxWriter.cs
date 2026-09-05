using System.Text.Json;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Events;
using CEBAS.Domain.Common;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Infrastructure.Services;

public class OutboxWriter : IOutboxWriter
{
    private readonly ApplicationDbContext _dbContext;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public OutboxWriter(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task EnqueueAsync<TEvent>(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        TEvent payload,
        Guid? actorId = null,
        Guid? recipientId = null,
        string? correlationId = null,
        string? causationId = null,
        CancellationToken cancellationToken = default) where TEvent : class
    {
        var eventId = Uuid7.New();
        var occurredAt = DateTimeOffset.UtcNow;

        var envelope = new OutboxEventEnvelope<TEvent>(
            eventId,
            eventType,
            aggregateType,
            aggregateId,
            occurredAt,
            actorId,
            recipientId,
            correlationId,
            causationId,
            payload
        );

        string serializedEnvelope = JsonSerializer.Serialize(envelope, JsonOptions);

        var outboxEvent = OutboxEvent.Create(
            eventType,
            aggregateType,
            aggregateId,
            serializedEnvelope,
            maxRetries: 5,
            correlationId: correlationId,
            causationId: causationId
        );

        await _dbContext.OutboxEvents.AddAsync(outboxEvent, cancellationToken);
    }
}
