using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CEBAS.Application.Abstractions;

namespace CEBAS.Api.Authentication;

public class CookieSessionAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "SessionCookie";
    public const string SessionCookieName = "cebas_session";

    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionTokenService _sessionTokenService;
    private readonly IDateTimeProvider _dateTimeProvider;

    public CookieSessionAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        ISessionRepository sessionRepository,
        ISessionTokenService sessionTokenService,
        IDateTimeProvider dateTimeProvider)
        : base(options, logger, encoder)
    {
        _sessionRepository = sessionRepository;
        _sessionTokenService = sessionTokenService;
        _dateTimeProvider = dateTimeProvider;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? rawToken = null;

        // 1. Try reading session token from HttpOnly cookie
        if (Request.Cookies.TryGetValue(SessionCookieName, out var cookieToken) && !string.IsNullOrWhiteSpace(cookieToken))
        {
            rawToken = cookieToken;
        }
        // 2. Fallback to Authorization: Bearer header for API integration flexibility
        else if (Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerValue = authHeader.ToString();
            if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                rawToken = headerValue["Bearer ".Length..].Trim();
            }
        }

        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return AuthenticateResult.NoResult();
        }

        // 3. Compute SHA-256 hash of the incoming raw token
        string tokenHash = _sessionTokenService.ComputeTokenHash(rawToken);

        // 4. Look up active session
        var session = await _sessionRepository.GetByTokenHashAsync(tokenHash);
        if (session == null || !session.IsActive(_dateTimeProvider.UtcNow) || session.User == null)
        {
            return AuthenticateResult.Fail("Session is invalid, expired, or has been revoked.");
        }

        // 5. Establish authenticated principal claims
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, session.User.Id.ToString()),
            new(ClaimTypes.Name, session.User.Username),
            new(ClaimTypes.Email, session.User.Email),
            new(ClaimTypes.Role, session.User.Role.ToString().ToUpperInvariant()),
            new(ClaimTypes.Sid, session.Id.ToString())
        };

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }
}
