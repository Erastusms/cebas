using MediatR;
using Microsoft.EntityFrameworkCore;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Entities;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Features.Media.GetContent;

public sealed record MediaContentResult(
    Stream Stream,
    string MimeType,
    string FileName
);

public sealed record GetMediaContentQuery(Guid MediaId) : IRequest<MediaContentResult>;

public sealed class GetMediaContentQueryHandler : IRequestHandler<GetMediaContentQuery, MediaContentResult>
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IObjectStorage _objectStorage;

    public GetMediaContentQueryHandler(
        ApplicationDbContext dbContext,
        IObjectStorage objectStorage)
    {
        _dbContext = dbContext;
        _objectStorage = objectStorage;
    }

    public async Task<MediaContentResult> Handle(GetMediaContentQuery request, CancellationToken cancellationToken)
    {
        var media = await _dbContext.Media
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Id == request.MediaId, cancellationToken);

        if (media == null || media.Status == MediaStatus.Deleted)
        {
            throw new NotFoundException("Media not found.");
        }

        if (media.Status != MediaStatus.Ready)
        {
            throw new ValidationException("Status", "Media is not ready for viewing.");
        }

        var stream = await _objectStorage.OpenReadAsync(media.StorageKey, cancellationToken);
        if (stream == null)
        {
            throw new NotFoundException("Media binary content was not found in storage.");
        }

        return new MediaContentResult(stream, media.MimeType, media.OriginalFileName);
    }
}
