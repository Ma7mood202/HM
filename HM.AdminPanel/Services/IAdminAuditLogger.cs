namespace HM.AdminPanel.Services;

public interface IAdminAuditLogger
{
    Task LogAsync(
        Guid    adminUserId,
        string  adminEmail,
        string  action,
        string  entityType,
        string? entityId,
        string? details,
        string? ipAddress,
        CancellationToken ct = default);
}
