using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using CEBAS.Application.Abstractions;
using CEBAS.Domain.Entities;

namespace CEBAS.Api.Services;

public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && Guid.TryParse(claim.Value, out var guid) ? guid : null;
        }
    }

    public Guid? SessionId
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.Sid);
            return claim != null && Guid.TryParse(claim.Value, out var guid) ? guid : null;
        }
    }

    public string? Username => User?.FindFirst(ClaimTypes.Name)?.Value;

    public UserRole? Role
    {
        get
        {
            var claim = User?.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<UserRole>(claim, true, out var role) ? role : null;
        }
    }

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
