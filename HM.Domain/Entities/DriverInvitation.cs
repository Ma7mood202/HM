namespace HM.Domain.Entities;

public class DriverInvitation
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; }
}
