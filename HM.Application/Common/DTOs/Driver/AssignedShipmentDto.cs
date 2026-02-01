using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Response model for assigned shipment data (driver view).
/// Note: Does NOT include price information.
/// </summary>
public class AssignedShipmentDto
{
    public Guid ShipmentId { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public decimal EstimatedWeight { get; set; }
    public string? Notes { get; set; }
    public ShipmentStatus Status { get; set; }
    public string? TruckPlateNumber { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
