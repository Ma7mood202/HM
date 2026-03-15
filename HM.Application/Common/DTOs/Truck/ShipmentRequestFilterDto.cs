using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Filter model for browsing shipment requests (open shipments).
/// </summary>
public class ShipmentRequestFilterDto
{
    public TruckType? RequiredTruckType { get; set; }
    public TruckType? TruckType { get; set; }
    /// <summary>Truck body type: Open, Closed, Refrigerated.</summary>
    public TruckBodyType? TruckBodyType { get; set; }
    public decimal? MinWeight { get; set; }
    public decimal? MaxWeight { get; set; }
    /// <summary>Parcel weight in tons.</summary>
    public decimal? ParcelWeightTon { get; set; }
    public string? FromRegion { get; set; }
    public string? ToRegion { get; set; }
    public Guid? PickupRegionId { get; set; }
    public Guid? DropoffRegionId { get; set; }
    public string? PickupLocationSearch { get; set; }
    public string? DropoffLocationSearch { get; set; }
    /// <summary>Driver latitude for "near me" filter.</summary>
    public double? Latitude { get; set; }
    /// <summary>Driver longitude for "near me" filter.</summary>
    public double? Longitude { get; set; }
    /// <summary>Radius in km for "near me" (used with Latitude/Longitude).</summary>
    public double? RadiusKm { get; set; }
}
