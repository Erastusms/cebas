using CEBAS.Application.Contracts.Events;

namespace CEBAS.Application.Abstractions.Search;

public interface ISearchProjectionService
{
    Task ProjectPostCreatedAsync(PostCreatedPayload payload, CancellationToken cancellationToken = default);
    Task ProjectPostDeletedAsync(Guid postId, CancellationToken cancellationToken = default);
    Task ProjectPostUpdatedAsync(PostUpdatedPayload payload, CancellationToken cancellationToken = default);
    Task ProjectProfileUpdatedAsync(ProfileUpdatedPayload payload, CancellationToken cancellationToken = default);
    Task ProjectUserUpdatedAsync(UserUpdatedPayload payload, CancellationToken cancellationToken = default);
    Task ProjectUserCreatedAsync(UserCreatedPayload payload, CancellationToken cancellationToken = default);
    Task ProjectPostHiddenAsync(Guid postId, CancellationToken cancellationToken = default);
    Task ProjectUserSuspendedAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ProjectUserReinstatedAsync(Guid userId, CancellationToken cancellationToken = default);
}
