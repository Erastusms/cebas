using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Notifications;
using CEBAS.Domain.Entities;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Notifications.GetNotifications;

public sealed record GetNotificationsQuery(
    Guid UserId,
    string? Cursor,
    int Limit = 20,
    bool UnreadOnly = false
) : IRequest<CursorPagedResult<NotificationResponseDto>>;

public sealed class GetNotificationsQueryHandler : IRequestHandler<GetNotificationsQuery, CursorPagedResult<NotificationResponseDto>>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<GetNotificationsQueryHandler> _logger;

    public GetNotificationsQueryHandler(
        ApplicationDbContext dbContext,
        ILogger<GetNotificationsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<CursorPagedResult<NotificationResponseDto>> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        int limit = Math.Clamp(request.Limit, 1, 50);

        // Fetch blocked users to exclude from notifications feed
        var blockedUserIds = await _dbContext.Blocks
            .AsNoTracking()
            .Where(b => b.BlockerId == request.UserId || b.BlockedId == request.UserId)
            .Select(b => b.BlockerId == request.UserId ? b.BlockedId : b.BlockerId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Include(n => n.Actor)
            .Where(n => n.RecipientId == request.UserId);

        if (blockedUserIds.Count > 0)
        {
            query = query.Where(n => !blockedUserIds.Contains(n.ActorId));
        }

        if (request.UnreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        // Keyset pagination cursor
        if (!string.IsNullOrWhiteSpace(request.Cursor))
        {
            var decoded = Cursor.Decode(request.Cursor);
            if (decoded != null)
            {
                query = query.Where(n =>
                    n.CreatedAt < decoded.CreatedAt ||
                    (n.CreatedAt == decoded.CreatedAt && n.Id.CompareTo(decoded.Id) < 0));
            }
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        // Resolve any historical or reply-targeted notifications to their parent post IDs
        var replyTargetIds = notifications
            .Where(n => n.TargetType == "REPLY" && n.TargetId.HasValue)
            .Select(n => n.TargetId!.Value)
            .Distinct()
            .ToList();

        var replyToPostMap = new Dictionary<Guid, Guid>();
        if (replyTargetIds.Count > 0)
        {
            replyToPostMap = await _dbContext.PostReplies
                .AsNoTracking()
                .Where(r => replyTargetIds.Contains(r.Id))
                .ToDictionaryAsync(r => r.Id, r => r.PostId, cancellationToken);
        }

        var dtos = notifications.Select(n =>
        {
            var targetId = n.TargetId;
            var targetType = n.TargetType;

            if (targetType == "REPLY" && targetId.HasValue && replyToPostMap.TryGetValue(targetId.Value, out var parentPostId))
            {
                targetId = parentPostId;
                targetType = "POST";
            }

            return new NotificationResponseDto(
                n.Id,
                new NotificationActorDto(
                    n.Actor!.Id,
                    n.Actor.Username,
                    n.Actor.DisplayName,
                    n.Actor.AvatarUrl,
                    n.Actor.IsVerified
                ),
                FormatNotificationType(n.Type),
                targetId,
                targetType,
                n.IsRead,
                n.ReadAt,
                n.CreatedAt
            );
        }).ToList();

        return CursorPagedResult<NotificationResponseDto>.Create(
            dtos,
            limit,
            dto => new Cursor(dto.CreatedAt, dto.Id)
        );
    }

    private static string FormatNotificationType(NotificationType type) => type switch
    {
        NotificationType.PostLiked => "POST_LIKED",
        NotificationType.PostReplied => "POST_REPLIED",
        NotificationType.ReplyLiked => "REPLY_LIKED",
        NotificationType.UserFollowed => "USER_FOLLOWED",
        NotificationType.UserMentioned => "USER_MENTIONED",
        _ => type.ToString().ToUpperInvariant()
    };
}
