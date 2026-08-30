using MediatR;

namespace CEBAS.Domain.Common;

/// <summary>
/// Marker interface for in-process Domain Events implementing MediatR INotification.
/// </summary>
public interface IDomainEvent : INotification
{
    DateTimeOffset OccurredAt { get; }
}
