using HM.Domain.Entities;
using HM.Infrastructure.Data;

namespace HM.AdminPanel.Services;

public class AdminAuditLogger : IAdminAuditLogger
{
    private readonly ApplicationDbContext _db;
    public AdminAuditLogger(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(
        Guid adminUserId, string adminEmail, string action, string entityType,
        string? entityId, string? details, string? ipAddress,
        CancellationToken ct = default)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id          = Guid.NewGuid(),
            AdminUserId = adminUserId,
            AdminEmail  = adminEmail,
            Action      = action,
            EntityType  = entityType,
            EntityId    = entityId,
            Details     = details,
            IpAddress   = ipAddress,
            CreatedAt   = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
