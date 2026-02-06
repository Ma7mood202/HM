using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Minimal tracking payload for driver (location if stored).
/// </summary>
public class DriverShipmentTrackingResponse
{
    public Guid ShipmentId { get; set; }
    public ShipmentStatus Status { get; set; }
    public double? CurrentLat { get; set; }
    public double? CurrentLng { get; set; }
    public DateTime? LastUpdatedAt { get; set; }
    public string? RoutePolyline { get; set; }
}
