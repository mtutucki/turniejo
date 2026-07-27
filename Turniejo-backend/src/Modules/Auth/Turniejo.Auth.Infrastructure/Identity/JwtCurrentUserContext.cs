using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using Turniejo.SharedKernel.Contracts;

namespace Turniejo.Auth.Infrastructure.Identity;

internal sealed class JwtCurrentUserContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserContext
{
    private ClaimsPrincipal? User =>
        httpContextAccessor.HttpContext?.User;

    public Guid UserId =>
        Guid.TryParse(User?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User?.FindFirstValue("sub"), out var id)
            ? id
            : Guid.Empty;

    public string Email =>
        User?.FindFirstValue(ClaimTypes.Email)
        ?? User?.FindFirstValue("email")
        ?? string.Empty;

    public bool IsAuthenticated =>
        User?.Identity?.IsAuthenticated ?? false;
}