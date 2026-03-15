using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Summary for shipment request list (tabs / statuses).
/// </summary>
public class ShipmentRequestSummaryResponse
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public ShipmentRequestStatus Status { get; set; }
    public TruckType TruckType { get; set; }
    public DateTime CreatedAt { get; set; }

    public string? PickupGovernorate { get; set; }
    public string? PickupRegion { get; set; }
    public string PickupAreaOrText { get; set; } = string.Empty;
    public string DropoffAreaOrText { get; set; } = string.Empty;

    public DateOnly? DeliveryDate { get; set; }
    public string DeliveryTimeWindow { get; set; } = string.Empty;

    public int OffersCount { get; set; }
    /// <summary>Amount due (accepted offer price or 0).</summary>
    public decimal AmountDue { get; set; }
}
