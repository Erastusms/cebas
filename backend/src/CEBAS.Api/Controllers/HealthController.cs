using Microsoft.AspNetCore.Mvc;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Domain.Exceptions;

namespace CEBAS.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public HealthController(IDateTimeProvider dateTimeProvider)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Basic liveness health endpoint.
    /// </summary>
    /// <response code="200">Returns service liveness status</response>
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "Healthy",
            timestamp = _dateTimeProvider.UtcNow,
            service = "CEBAS API",
            version = "v1"
        });
    }

    /// <summary>
    /// Ping endpoint to verify API v1 routing.
    /// </summary>
    /// <response code="200">Returns pong response</response>
    [HttpGet("api/v1/ping")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public IActionResult Ping()
    {
        var response = ApiResponse<object>.Ok(
            new { message = "pong", timestamp = _dateTimeProvider.UtcNow },
            "API v1 is operational"
        );
        return Ok(response);
    }

    /// <summary>
    /// Development endpoint to verify RFC 7807 Problem Details handling.
    /// </summary>
    [HttpGet("api/v1/error-test")]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status500InternalServerError)]
    public IActionResult TestError([FromQuery] string type = "validation")
    {
        switch (type.ToLowerInvariant())
        {
            case "validation":
                throw new ValidationException(new Dictionary<string, string[]>
                {
                    { "username", ["Username must be between 3 and 30 characters."] },
                    { "email", ["Email address is invalid."] }
                });

            case "notfound":
                throw new NotFoundException("User", "usr_019800000000");

            case "conflict":
                throw new ConflictException("A user with this handle or email already exists.");

            case "unauthorized":
                throw new UnauthorizedException("Invalid authentication credentials.");

            case "forbidden":
                throw new ForbiddenException("You do not have permission to perform this moderation action.");

            default:
                throw new InvalidOperationException("Demonstration unhandled internal server error.");
        }
    }
}
