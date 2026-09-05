namespace CEBAS.Application.Abstractions;

public interface IOutboxWriter
{
    Task EnqueueAsync<TEvent>(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        TEvent payload,
        Guid? actorId = null,
        Guid? recipientId = null,
        string? correlationId = null,
        string? causationId = null,
        CancellationToken cancellationToken = default) where TEvent : class;
}
