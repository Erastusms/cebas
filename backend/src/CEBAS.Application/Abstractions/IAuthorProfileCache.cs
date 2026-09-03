using CEBAS.Application.Contracts.Posts;

namespace CEBAS.Application.Abstractions;

/// <summary>
/// Cache abstraction for author identity metadata (handle, display name, avatar, verified status)
/// to eliminate redundant user lookups during timeline projections.
/// </summary>
public interface IAuthorProfileCache
{
    Task<Dictionary<Guid, PostAuthorDto>> GetAuthorsAsync(IEnumerable<Guid> authorIds, CancellationToken cancellationToken = default);
    void InvalidateAuthor(Guid authorId);
}
