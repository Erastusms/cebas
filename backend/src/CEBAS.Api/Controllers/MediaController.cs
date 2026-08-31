using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Authentication;
using CEBAS.Api.Features.Media.Confirm;
using CEBAS.Api.Features.Media.GetContent;
using CEBAS.Api.Features.Media.Upload;
using CEBAS.Api.Features.Media.UploadBinary;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Media;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
public class MediaController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICurrentUser _currentUser;

    public MediaController(
        ISender sender,
        ICurrentUser currentUser)
    {
        _sender = sender;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Generates a direct upload target URL (pre-signed PUT URL) for media uploading.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPost("api/v1/media/upload-url")]
    [ProducesResponseType(typeof(ApiResponse<CreateMediaUploadResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CreateUploadUrl([FromBody] CreateMediaUploadRequest request, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to request upload targets.");
        }

        var command = new CreateMediaUploadCommand(
            _currentUser.UserId.Value,
            request.FileName,
            request.ContentType,
            request.FileSize);

        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<CreateMediaUploadResponse>.Ok(result, "Upload target generated successfully."));
    }

    /// <summary>
    /// Confirms that a media binary upload completed to storage and transitions media to READY.
    /// </summary>
    [Authorize(AuthenticationSchemes = CookieSessionAuthenticationHandler.SchemeName)]
    [HttpPost("api/v1/media/{id}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<MediaResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ConfirmMediaUpload([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        if (!_currentUser.UserId.HasValue)
        {
            throw new UnauthorizedException("Authenticated user context is required to confirm media uploads.");
        }

        var command = new ConfirmMediaUploadCommand(_currentUser.UserId.Value, id);
        var result = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<MediaResponse>.Ok(result, "Media upload confirmed successfully."));
    }

    /// <summary>
    /// Direct binary upload target endpoint for local development storage adapter.
    /// Emulates direct-to-S3 pre-signed PUT binary transfer.
    /// </summary>
    [HttpPut("api/v1/media/upload")]
    [DisableRequestSizeLimit]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadBinary([FromQuery] string? key, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ValidationException("key", "Storage key query parameter is required.");
        }

        var contentType = Request.ContentType ?? "application/octet-stream";
        var command = new UploadBinaryCommand(key, Request.Body, contentType);
        await _sender.Send(command, cancellationToken);

        return Ok(new { success = true, message = "Binary uploaded successfully." });
    }

    /// <summary>
    /// Controlled static media access endpoint for retrieving and streaming ready media images.
    /// </summary>
    [HttpGet("api/v1/media/{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMedia([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetMediaContentQuery(id), cancellationToken);

        // Add caching headers for optimal media delivery
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(result.Stream, result.MimeType, result.FileName);
    }
}
