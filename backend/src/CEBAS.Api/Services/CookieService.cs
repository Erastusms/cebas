using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using CEBAS.Api.Authentication;

namespace CEBAS.Api.Services;

public interface ICookieService
{
    void SetSessionCookie(HttpResponse response, string rawToken, DateTimeOffset expiresAt);
    void ClearSessionCookie(HttpResponse response);
    string? GetSessionToken(HttpRequest request);
}

public class CookieService : ICookieService
{
    private readonly IWebHostEnvironment _environment;

    public CookieService(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    public void SetSessionCookie(HttpResponse response, string rawToken, DateTimeOffset expiresAt)
    {
        bool isHttps = response.HttpContext.Request.IsHttps;
        bool isProduction = _environment.IsProduction();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = isHttps || isProduction,
            Path = "/",
            Expires = expiresAt,
            IsEssential = true
        };

        response.Cookies.Append(CookieSessionAuthenticationHandler.SessionCookieName, rawToken, cookieOptions);
    }

    public void ClearSessionCookie(HttpResponse response)
    {
        bool isHttps = response.HttpContext.Request.IsHttps;
        bool isProduction = _environment.IsProduction();

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
            Secure = isHttps || isProduction,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(-1),
            IsEssential = true
        };

        response.Cookies.Delete(CookieSessionAuthenticationHandler.SessionCookieName, cookieOptions);
    }

    public string? GetSessionToken(HttpRequest request)
    {
        if (request.Cookies.TryGetValue(CookieSessionAuthenticationHandler.SessionCookieName, out var cookieToken) &&
            !string.IsNullOrWhiteSpace(cookieToken))
        {
            return cookieToken;
        }

        if (request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var headerValue = authHeader.ToString();
            if (headerValue.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return headerValue["Bearer ".Length..].Trim();
            }
        }

        return null;
    }
}
