using System.Security.Claims;

namespace Hm.WebApi.Extensions;

/// <summary>
/// Extensions to read user identity from JWT claims.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }
}
