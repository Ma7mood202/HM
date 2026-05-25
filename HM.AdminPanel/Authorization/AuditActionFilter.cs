using System.Security.Claims;
using System.Text.Json;
using HM.AdminPanel.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HM.AdminPanel.Authorization;

public class AuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> WriteVerbs =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE", "PATCH" };

    private readonly IAdminAuditLogger _logger;

    public AuditActionFilter(IAdminAuditLogger logger) => _logger = logger;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var verb = context.HttpContext.Request.Method;
        var executed = await next();

        if (!WriteVerbs.Contains(verb)) return;
        if (context.HttpContext.User?.Identity?.IsAuthenticated != true) return;

        var idClaim    = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var emailClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.Email) ?? "";
        if (!Guid.TryParse(idClaim, out var adminId)) return;

        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        var action     = context.RouteData.Values["action"]?.ToString() ?? "";
        var entityId   = context.RouteData.Values["id"]?.ToString();
        var ip         = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        var details = JsonSerializer.Serialize(new
        {
            statusCode = context.HttpContext.Response.StatusCode,
            hasException = executed.Exception != null,
            exceptionMessage = executed.Exception?.Message
        });

        await _logger.LogAsync(
            adminUserId: adminId,
            adminEmail:  emailClaim,
            action:      $"{controller}.{action}",
            entityType:  controller,
            entityId:    entityId,
            details:     details,
            ipAddress:   ip);
    }
}
