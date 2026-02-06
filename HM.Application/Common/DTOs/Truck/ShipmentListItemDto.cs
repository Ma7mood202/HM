using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Response model for shipment list item (truck account view).
/// Used for both open shipment requests (ShipmentId/Status/Price/Driver/Truck null or zero) and my shipments.
/// </summary>
public class ShipmentListItemDto
{
    public Guid ShipmentRequestId { get; set; }
    public Guid? ShipmentId { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public TruckType? RequiredTruckType { get; set; }
    public decimal EstimatedWeight { get; set; }
    public int OffersCount { get; set; }
    public ShipmentStatus? Status { get; set; }
    public decimal Price { get; set; }
    public string? DriverName { get; set; }
    public string? TruckPlateNumber { get; set; }
    public DateTime CreatedAt { get; set; }
}
