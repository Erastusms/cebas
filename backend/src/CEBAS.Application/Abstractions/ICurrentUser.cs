using CEBAS.Domain.Entities;

namespace CEBAS.Application.Abstractions;

public interface ICurrentUser
{
    Guid? UserId { get; }
    Guid? SessionId { get; }
    string? Username { get; }
    UserRole? Role { get; }
    bool IsAuthenticated { get; }
}
