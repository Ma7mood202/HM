using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Request for creating a shipment (wizard submit).
/// </summary>
public class CreateShipmentRequestRequest
{
    public TruckType TruckType { get; set; }

    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;

    public string PickupAddressText { get; set; } = string.Empty;
    public string? PickupArea { get; set; }
    public double? PickupLat { get; set; }
    public double? PickupLng { get; set; }

    public string DropoffAddressText { get; set; } = string.Empty;
    public string? DropoffArea { get; set; }
    public double? DropoffLat { get; set; }
    public double? DropoffLng { get; set; }

    public string? ParcelDescription { get; set; }
    public string? ParcelType { get; set; }
    public decimal ParcelWeightKg { get; set; }
    public string? ParcelSize { get; set; }
    public int ParcelCount { get; set; } = 1;

    public DateOnly DeliveryDate { get; set; }
    public TimeOnly? DeliveryTimeFrom { get; set; }
    public TimeOnly? DeliveryTimeTo { get; set; }

    public string? Notes { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
}
