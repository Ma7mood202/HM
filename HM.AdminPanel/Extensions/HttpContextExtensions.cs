using System.Security.Claims;

namespace HM.AdminPanel.Extensions;

public static class HttpContextExtensions
{
    public static Guid? CurrentAdminId(this HttpContext ctx)
    {
        var raw = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string CurrentAdminEmail(this HttpContext ctx) =>
        ctx.User.FindFirstValue(ClaimTypes.Email) ?? "";
}
