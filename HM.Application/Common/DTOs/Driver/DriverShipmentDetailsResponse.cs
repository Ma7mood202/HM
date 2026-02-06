using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Full shipment details for driver details screen (map + key-value).
/// </summary>
public class DriverShipmentDetailsResponse
{
    public Guid ShipmentId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }

    public string PickupAddressText { get; set; } = string.Empty;
    public double? PickupLat { get; set; }
    public double? PickupLng { get; set; }

    public string DropoffAddressText { get; set; } = string.Empty;
    public double? DropoffLat { get; set; }
    public double? DropoffLng { get; set; }

    public string? MerchantName { get; set; }
    public string? MerchantPhone { get; set; }

    public string? RecipientName { get; set; }
    public string? RecipientPhone { get; set; }

    public string? ParcelDescription { get; set; }
    public string? ParcelType { get; set; }
    public decimal WeightKg { get; set; }
    public int ParcelCount { get; set; }
    public string? ParcelSize { get; set; }

    public DateOnly? DeliveryDate { get; set; }
    public string DeliveryTimeWindow { get; set; } = string.Empty;

    public PaymentMethod? PaymentMethod { get; set; }
    public decimal? OfferPrice { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
