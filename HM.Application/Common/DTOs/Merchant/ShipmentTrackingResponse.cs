using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Tracking payload for map view (minimal: status + optional location).
/// </summary>
public class ShipmentTrackingResponse
{
    public Guid ShipmentId { get; set; }
    public ShipmentStatus Status { get; set; }
    public double? CurrentLat { get; set; }
    public double? CurrentLng { get; set; }
    public string? RoutePolyline { get; set; }
    public DateTime? LastUpdatedAt { get; set; }

    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? TruckPlateNumber { get; set; }
    public TruckType? TruckType { get; set; }
}
