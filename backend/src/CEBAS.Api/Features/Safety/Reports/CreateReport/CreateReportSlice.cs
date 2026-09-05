using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Contracts.Reports;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;
using ValidationException = CEBAS.Domain.Exceptions.ValidationException;

namespace CEBAS.Api.Features.Safety.Reports.CreateReport;

public sealed record CreateReportCommand(
    Guid ReporterUserId,
    Guid? TargetPostId,
    Guid? TargetUserId,
    string Category,
    string? Description
) : IRequest<ReportResponse>;

public sealed class CreateReportCommandValidator : AbstractValidator<CreateReportCommand>
{
    public CreateReportCommandValidator()
    {
        RuleFor(x => x.ReporterUserId)
            .NotEmpty().WithMessage("Reporter user ID is required.");

        RuleFor(x => x.Category)
            .NotEmpty().WithMessage("Report category is required.")
            .Must(BeValidCategory).WithMessage("Invalid report category. Allowed values: SPAM, HARASSMENT, HATE_SPEECH, INAPPROPRIATE_CONTENT.");

        RuleFor(x => x)
            .Must(x => (x.TargetPostId.HasValue && !x.TargetUserId.HasValue) || (!x.TargetPostId.HasValue && x.TargetUserId.HasValue))
            .WithMessage("A report must target either a post or a user, but not both.");

        RuleFor(x => x.Description)
            .MaximumLength(Report.MaxReasonLength)
            .WithMessage($"Report description cannot exceed {Report.MaxReasonLength} characters.");
    }

    private static bool BeValidCategory(string category)
    {
        return Enum.TryParse<ReportCategory>(category, true, out _);
    }
}

public sealed class CreateReportCommandHandler : IRequestHandler<CreateReportCommand, ReportResponse>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IOutboxWriter _outboxWriter;
    private readonly ILogger<CreateReportCommandHandler> _logger;

    public CreateReportCommandHandler(
        ApplicationDbContext dbContext,
        IOutboxWriter outboxWriter,
        ILogger<CreateReportCommandHandler> logger)
    {
        _dbContext = dbContext;
        _outboxWriter = outboxWriter;
        _logger = logger;
    }

    public async Task<ReportResponse> Handle(CreateReportCommand request, CancellationToken cancellationToken)
    {
        // 1. Verify reporter eligibility
        var reporter = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.ReporterUserId, cancellationToken);

        if (reporter == null)
        {
            throw new UnauthorizedException("Reporter account does not exist.");
        }

        if (reporter.IsSuspended)
        {
            throw new ForbiddenException("Your account has been suspended and cannot submit reports.");
        }

        // 2. Target validation & duplicate suppression
        Guid? targetPostId = null;
        Guid? targetUserId = null;

        if (request.TargetPostId.HasValue && request.TargetPostId.Value != Guid.Empty)
        {
            var post = await _dbContext.Posts
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == request.TargetPostId.Value, cancellationToken);

            if (post == null)
            {
                throw new NotFoundException($"Target post '{request.TargetPostId.Value}' was not found.");
            }

            if (post.AuthorId == request.ReporterUserId)
            {
                throw new ValidationException("TargetPostId", "You cannot report your own post.");
            }

            var duplicatePending = await _dbContext.Reports
                .AsNoTracking()
                .AnyAsync(r => r.TargetPostId == request.TargetPostId.Value &&
                               r.ReporterUserId == request.ReporterUserId &&
                               r.Status == ReportStatus.PENDING,
                          cancellationToken);

            if (duplicatePending)
            {
                throw new ConflictException("You have already submitted a pending report for this post. Our moderation team is reviewing it.");
            }

            targetPostId = request.TargetPostId.Value;
        }
        else if (request.TargetUserId.HasValue && request.TargetUserId.Value != Guid.Empty)
        {
            var targetUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.TargetUserId.Value, cancellationToken);

            if (targetUser == null)
            {
                throw new NotFoundException($"Target user '{request.TargetUserId.Value}' was not found.");
            }

            if (targetUser.Id == request.ReporterUserId)
            {
                throw new ValidationException("TargetUserId", "You cannot report your own account.");
            }

            var duplicatePending = await _dbContext.Reports
                .AsNoTracking()
                .AnyAsync(r => r.TargetUserId == request.TargetUserId.Value &&
                               r.ReporterUserId == request.ReporterUserId &&
                               r.Status == ReportStatus.PENDING,
                          cancellationToken);

            if (duplicatePending)
            {
                throw new ConflictException("You have already submitted a pending report for this user. Our moderation team is reviewing it.");
            }

            targetUserId = request.TargetUserId.Value;
        }
        else
        {
            throw new ValidationException("Target", "A report must target either a post or a user.");
        }

        // 3. Create Report Entity
        var category = Enum.Parse<ReportCategory>(request.Category, true);
        var report = Report.Create(
            request.ReporterUserId,
            targetPostId,
            targetUserId,
            category,
            request.Description);

        await _dbContext.Reports.AddAsync(report, cancellationToken);

        // 4. Enqueue transactional outbox event
        await _outboxWriter.EnqueueAsync(
            "ReportCreated",
            "Report",
            report.Id,
            new
            {
                report.Id,
                report.ReporterUserId,
                report.TargetPostId,
                report.TargetUserId,
                Category = report.Category.ToString(),
                Status = report.Status.ToString(),
                report.Reason,
                report.CreatedAt
            },
            actorId: report.ReporterUserId,
            cancellationToken: cancellationToken);

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Safety report {ReportId} created by user {ReporterUserId} against {TargetType} {TargetId}",
            report.Id, report.ReporterUserId, targetPostId.HasValue ? "Post" : "User", targetPostId ?? targetUserId);

        return new ReportResponse(
            report.Id,
            report.ReporterUserId,
            report.TargetPostId,
            report.TargetUserId,
            report.Category.ToString(),
            report.Status.ToString(),
            report.Reason,
            report.CreatedAt,
            report.ResolvedAt,
            report.ResolvedByUserId
        );
    }
}
