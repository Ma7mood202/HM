using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Request for creating a shipment (wizard submit).
/// Weight in tons; pickup/dropoff by governorate and region.
/// </summary>
public class CreateShipmentRequestRequest
{
    public TruckType TruckType { get; set; }
    public TruckBodyType? TruckBodyType { get; set; }

    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;
    public string? ReceiverPhoneNumber { get; set; }

    public Guid? PickupGovernorateId { get; set; }
    public Guid? PickupRegionId { get; set; }
    public string PickupAddressText { get; set; } = string.Empty;
    public string? PickupArea { get; set; }

    public Guid? DropoffGovernorateId { get; set; }
    public Guid? DropoffRegionId { get; set; }
    public string DropoffAddressText { get; set; } = string.Empty;
    public string? DropoffArea { get; set; }

    public string? ParcelDescription { get; set; }
    public string? ParcelType { get; set; }
    /// <summary>Weight in tons.</summary>
    public decimal ParcelWeightTon { get; set; }
    public string? ParcelSize { get; set; }
    public int ParcelCount { get; set; } = 1;
    public bool IsFragile { get; set; }

    public DateOnly DeliveryDate { get; set; }
    public TimeOnly? DeliveryTimeFrom { get; set; }
    public TimeOnly? DeliveryTimeTo { get; set; }

    public string? Notes { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
}
