using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Shipment;

/// <summary>
/// Response model for full shipment details.
/// </summary>
public class ShipmentDetailsDto
{
    public Guid Id { get; set; }
    public Guid ShipmentRequestId { get; set; }
    public Guid AcceptedOfferId { get; set; }
    public Guid TruckId { get; set; }
    public Guid? DriverProfileId { get; set; }

    // Shipment Request Details
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public TruckType RequiredTruckType { get; set; }
    public decimal EstimatedWeight { get; set; }
    public string? Notes { get; set; }

    // Offer Details
    public decimal Price { get; set; }

    // Truck Details
    public string TruckPlateNumber { get; set; } = string.Empty;

    // Driver Details
    public string? DriverName { get; set; }

    // Status
    public ShipmentStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
