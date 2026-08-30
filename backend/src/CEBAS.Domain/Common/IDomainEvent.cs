namespace CEBAS.Domain.Common;

/// <summary>
/// Marker contract for domain events dispatched upon aggregate state transitions.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTimeOffset OccurredOn { get; }
}
