namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Response model for driver invitation data.
/// </summary>
public class DriverInvitationDto
{
    public Guid Id { get; set; }
    public Guid ShipmentId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string InvitationUrl { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
