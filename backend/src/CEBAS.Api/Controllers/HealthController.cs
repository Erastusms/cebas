using Microsoft.AspNetCore.Mvc;
using CEBAS.Application.Abstractions;
using CEBAS.Application.Common;
using CEBAS.Domain.Exceptions;
using CEBAS.Infrastructure.Persistence;

namespace CEBAS.Api.Controllers;

[ApiController]
public class HealthController : ControllerBase
{
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ApplicationDbContext _dbContext;

    public HealthController(IDateTimeProvider dateTimeProvider, ApplicationDbContext dbContext)
    {
        _dateTimeProvider = dateTimeProvider;
        _dbContext = dbContext;
    }

    /// <summary>
    /// Lightweight process liveness check (/healthz).
    /// Determines whether the process is alive without expensive dependencies.
    /// </summary>
    /// <response code="200">Process is alive</response>
    [HttpGet("healthz")]
    [HttpGet("health")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetLiveness()
    {
        return Ok(new
        {
            status = "Healthy",
            process = "alive",
            timestamp = _dateTimeProvider.UtcNow,
            service = "CEBAS API",
            version = "v1"
        });
    }

    /// <summary>
    /// Production readiness check (/readyz).
    /// Verifies critical dependencies (database connectivity) before routing traffic.
    /// </summary>
    /// <response code="200">Application is ready to receive traffic</response>
    /// <response code="503">Critical dependencies are unavailable</response>
    [HttpGet("readyz")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness(CancellationToken cancellationToken)
    {
        try
        {
            var dbOk = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (!dbOk)
            {
                return StatusCode(StatusCodes.Status503ServiceUnavailable, new
                {
                    status = "Unhealthy",
                    ready = false,
                    checks = new { database = "Unhealthy" },
                    timestamp = _dateTimeProvider.UtcNow
                });
            }

            return Ok(new
            {
                status = "Ready",
                ready = true,
                checks = new { database = "Healthy" },
                timestamp = _dateTimeProvider.UtcNow,
                service = "CEBAS API",
                version = "v1"
            });
        }
        catch (Exception)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status = "Unhealthy",
                ready = false,
                checks = new { database = "Unhealthy" },
                timestamp = _dateTimeProvider.UtcNow
            });
        }
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
