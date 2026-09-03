using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Posts;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Infrastructure.Services;

/// <summary>
/// Memory-cached provider for author profile metadata (handle, display name, avatar, is_verified).
/// Caches safe public identity details with sliding expiration to reduce DB overhead in feed paths.
/// </summary>
public class AuthorProfileCache : IAuthorProfileCache
{
    private readonly IMemoryCache _memoryCache;
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<AuthorProfileCache> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    public AuthorProfileCache(
        IMemoryCache memoryCache,
        ApplicationDbContext dbContext,
        ILogger<AuthorProfileCache> logger)
    {
        _memoryCache = memoryCache;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<Dictionary<Guid, PostAuthorDto>> GetAuthorsAsync(IEnumerable<Guid> authorIds, CancellationToken cancellationToken = default)
    {
        var distinctIds = authorIds.Distinct().ToList();
        var result = new Dictionary<Guid, PostAuthorDto>();
        var missingIds = new List<Guid>();

        foreach (var id in distinctIds)
        {
            var cacheKey = $"author_profile_{id}";
            if (_memoryCache.TryGetValue<PostAuthorDto>(cacheKey, out var cachedAuthor) && cachedAuthor != null)
            {
                result[id] = cachedAuthor;
            }
            else
            {
                missingIds.Add(id);
            }
        }

        if (missingIds.Count > 0)
        {
            var loadedAuthors = await _dbContext.Users
                .AsNoTracking()
                .Where(u => missingIds.Contains(u.Id))
                .Select(u => new PostAuthorDto(u.Id, u.Username, u.DisplayName, u.AvatarUrl, u.IsVerified))
                .ToListAsync(cancellationToken);

            foreach (var author in loadedAuthors)
            {
                var cacheKey = $"author_profile_{author.Id}";
                _memoryCache.Set(cacheKey, author, CacheDuration);
                result[author.Id] = author;
            }
        }

        return result;
    }

    public void InvalidateAuthor(Guid authorId)
    {
        _memoryCache.Remove($"author_profile_{authorId}");
    }
}
