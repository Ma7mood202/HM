using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Full details for "تفاصيل الطلب" + map pins.
/// </summary>
public class ShipmentRequestDetailsResponse
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public ShipmentRequestStatus Status { get; set; }
    public TruckType TruckType { get; set; }
    public DateTime CreatedAt { get; set; }

    public string PickupAddressText { get; set; } = string.Empty;
    public string? PickupArea { get; set; }
    public double? PickupLat { get; set; }
    public double? PickupLng { get; set; }

    public string DropoffAddressText { get; set; } = string.Empty;
    public string? DropoffArea { get; set; }
    public double? DropoffLat { get; set; }
    public double? DropoffLng { get; set; }

    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;

    public string? ParcelDescription { get; set; }
    public string? ParcelType { get; set; }
    public decimal ParcelWeightKg { get; set; }
    public string? ParcelSize { get; set; }
    public int ParcelCount { get; set; }

    public DateOnly? DeliveryDate { get; set; }
    public TimeOnly? DeliveryTimeFrom { get; set; }
    public TimeOnly? DeliveryTimeTo { get; set; }
    public string DeliveryTimeWindow { get; set; } = string.Empty;

    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }

    public int OffersCount { get; set; }
    public AcceptedOfferSummary? AcceptedOffer { get; set; }
    public AssignedDriverSummary? AssignedDriver { get; set; }
}

public class AcceptedOfferSummary
{
    public Guid OfferId { get; set; }
    public decimal Price { get; set; }
    public string? TruckAccountName { get; set; }
}

public class AssignedDriverSummary
{
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? TruckPlateNumber { get; set; }
}
