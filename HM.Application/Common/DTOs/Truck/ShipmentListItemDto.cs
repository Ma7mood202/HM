using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Response model for shipment list item (truck account view).
/// </summary>
public class ShipmentListItemDto
{
    public Guid ShipmentId { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public decimal Price { get; set; }
    public string? DriverName { get; set; }
    public string? TruckPlateNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
