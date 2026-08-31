using MediatR;
using Microsoft.Extensions.Logging;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Features.Media.UploadBinary;

public sealed record UploadBinaryCommand(
    string StorageKey,
    Stream Content,
    string MimeType
) : IRequest<bool>;

public sealed class UploadBinaryCommandHandler : IRequestHandler<UploadBinaryCommand, bool>
{
    private readonly IObjectStorage _objectStorage;
    private readonly ILogger<UploadBinaryCommandHandler> _logger;

    public UploadBinaryCommandHandler(
        IObjectStorage objectStorage,
        ILogger<UploadBinaryCommandHandler> logger)
    {
        _objectStorage = objectStorage;
        _logger = logger;
    }

    public async Task<bool> Handle(UploadBinaryCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StorageKey))
        {
            throw new ValidationException("StorageKey", "Storage key parameter is required.");
        }

        if (string.IsNullOrWhiteSpace(request.MimeType))
        {
            throw new ValidationException("MimeType", "Content-Type header is required.");
        }

        await _objectStorage.SaveAsync(request.StorageKey, request.Content, request.MimeType, cancellationToken);
        _logger.LogInformation("Binary content written to storage key: {StorageKey}", request.StorageKey);

        return true;
    }
}
