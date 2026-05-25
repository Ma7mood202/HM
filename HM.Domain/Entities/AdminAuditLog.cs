namespace HM.Domain.Entities;

public class AdminAuditLog
{
    public Guid     Id          { get; set; }
    public Guid     AdminUserId { get; set; }
    public string   AdminEmail  { get; set; } = string.Empty;
    public string   Action      { get; set; } = string.Empty;
    public string   EntityType  { get; set; } = string.Empty;
    public string?  EntityId    { get; set; }
    public string?  Details     { get; set; }
    public string?  IpAddress   { get; set; }
    public DateTime CreatedAt   { get; set; }
}
