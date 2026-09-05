using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Reports;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Safety.Moderation.UnsuspendUser;

public sealed record UnsuspendUserCommand(
    Guid UserId,
    Guid AdminUserId,
    string? Reason
) : IRequest<UnsuspendUserResponse>;

public sealed class UnsuspendUserCommandValidator : AbstractValidator<UnsuspendUserCommand>
{
    public UnsuspendUserCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty().WithMessage("User ID is required.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("Admin user ID is required.");

        RuleFor(x => x.Reason)
            .MaximumLength(1000).WithMessage("Reason cannot exceed 1000 characters.");
    }
}

public sealed class UnsuspendUserCommandHandler : IRequestHandler<UnsuspendUserCommand, UnsuspendUserResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<UnsuspendUserCommandHandler> _logger;

    public UnsuspendUserCommandHandler(
        ApplicationDbContext dbContext,
        IOutboxWriter outboxWriter,
        ILogger<UnsuspendUserCommandHandler> logger)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task<UnsuspendUserResponse> Handle(UnsuspendUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        if (user == null)
        {
            throw new NotFoundException($"User with ID '{request.UserId}' was not found.");
        }

        var now = DateTimeOffset.UtcNow;

        if (!user.IsSuspended)
        {
            return new UnsuspendUserResponse(
                user.Id,
                user.Username,
                $"User @{user.Username} is not suspended.",
                now
            );
        }

        // 1. Reinstate user
        user.Reinstate();

        // 2. Restore any posts authored by user that were hidden by moderation
        var hiddenPosts = await _dbContext.Posts
            .Where(p => p.AuthorId == user.Id && p.IsHidden)
            .ToListAsync(cancellationToken);

        foreach (var post in hiddenPosts)
        {
            post.Restore();
        }

        // 3. Write Moderation Audit Log
        var audit = ModerationAuditLog.Create(
            request.AdminUserId,
            "USER_UNSUSPENDED",
            "User",
            user.Id,
            request.Reason ?? "Account unsuspended and posts restored by administrator."
        );
        await _dbContext.ModerationAuditLogs.AddAsync(audit, cancellationToken);

        // 4. Enqueue Outbox event
        await _outboxWriter.EnqueueAsync(
            "UserReinstated",
            "User",
            user.Id,
            new
            {
                UserId = user.Id,
                Username = user.Username,
                AdminUserId = request.AdminUserId,
                RestoredPostsCount = hiddenPosts.Count,
                Timestamp = now
            },
            actorId: request.AdminUserId,
            cancellationToken: cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        string message = $"User @{user.Username} has been unsuspended and {hiddenPosts.Count} hidden post(s) restored.";
        _logger.LogInformation("admin.unsuspend: User {UserId} (@{Username}) unsuspended by {AdminId}. {RestoredCount} posts restored",
            user.Id, user.Username, request.AdminUserId, hiddenPosts.Count);

        return new UnsuspendUserResponse(
            user.Id,
            user.Username,
            message,
            now
        );
    }
}
