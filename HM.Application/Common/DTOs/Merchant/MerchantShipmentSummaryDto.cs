using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>Summary of a merchant's accepted-and-active shipments for the tracking list.</summary>
public class MerchantShipmentSummaryDto
{
    public Guid ShipmentId { get; set; }
    public Guid ShipmentRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string? PickupRegion { get; set; }
    public string? DropoffRegion { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? DriverAvatarUrl { get; set; }
    public string? TruckPlateNumber { get; set; }
    public TruckType? TruckType { get; set; }
    public decimal Price { get; set; }
    public double? CurrentLat { get; set; }
    public double? CurrentLng { get; set; }
    public DateTime? LastLocationUpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
