using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Filter model for browsing shipment requests.
/// </summary>
public class ShipmentRequestFilterDto
{
    public TruckType? RequiredTruckType { get; set; }
    public decimal? MinWeight { get; set; }
    public decimal? MaxWeight { get; set; }
    public string? PickupLocationSearch { get; set; }
    public string? DropoffLocationSearch { get; set; }
}
