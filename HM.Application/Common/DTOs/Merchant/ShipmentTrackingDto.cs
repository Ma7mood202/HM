using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Response model for shipment tracking information.
/// </summary>
public class ShipmentTrackingDto
{
    public Guid ShipmentId { get; set; }
    public ShipmentStatus Status { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string? DriverName { get; set; }
    public string? TruckPlateNumber { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
