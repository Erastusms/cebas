using MediatR;
using Microsoft.AspNetCore.Mvc;
using CEBAS.Api.Features.Auth.Login;
using CEBAS.Api.Features.Auth.Logout;
using CEBAS.Api.Features.Auth.Register;
using CEBAS.Api.Services;
using CEBAS.Application.Common;
using CEBAS.Application.Contracts.Users;

using Microsoft.AspNetCore.RateLimiting;
using CEBAS.Api.RateLimiting;

namespace CEBAS.Api.Controllers;

[ApiController]
[EnableRateLimiting(RateLimitingRegistration.AuthenticationPolicy)]
public class AuthController : ControllerBase
{
    private readonly ISender _sender;
    private readonly ICookieService _cookieService;

    public AuthController(ISender sender, ICookieService cookieService)
    {
        _sender = sender;
        _cookieService = cookieService;
    }

    /// <summary>
    /// Registers a new user account with unique username and email.
    /// </summary>
    [HttpPost("api/v1/auth/register")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command, CancellationToken cancellationToken)
    {
        var response = await _sender.Send(command, cancellationToken);
        return Ok(ApiResponse<CurrentUserResponse>.Ok(response, "Registration successful."));
    }

    /// <summary>
    /// Authenticates a user and sets a secure HttpOnly session cookie.
    /// </summary>
    [HttpPost("api/v1/auth/login")]
    [ProducesResponseType(typeof(ApiResponse<CurrentUserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetailsResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        string? userAgent = Request.Headers.UserAgent.ToString();
        string? ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        var command = new LoginCommand(request.Identifier, request.Password, userAgent, ipAddress);
        var result = await _sender.Send(command, cancellationToken);

        // Deliver session token securely strictly via HttpOnly cookie
        _cookieService.SetSessionCookie(Response, result.RawSessionToken, result.SessionExpiresAt);

        return Ok(ApiResponse<CurrentUserResponse>.Ok(result.User, "Login successful."));
    }

    /// <summary>
    /// Logs out the user, revoking the active session and clearing the auth cookie.
    /// </summary>
    [HttpPost("api/v1/auth/logout")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        string? rawToken = _cookieService.GetSessionToken(Request);
        await _sender.Send(new LogoutCommand(rawToken), cancellationToken);
        _cookieService.ClearSessionCookie(Response);

        return Ok(ApiResponse<object>.Ok(new { }, "Logged out successfully."));
    }
}

public sealed record LoginRequest(string Identifier, string Password);
