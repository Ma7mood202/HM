using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class ShipmentRequest
{
    public Guid Id { get; set; }
    public Guid MerchantProfileId { get; set; }
    /// <summary>Public reference e.g. "HM474936". Generated in Infrastructure.</summary>
    public string RequestNumber { get; set; } = string.Empty;

    public TruckType RequiredTruckType { get; set; }

    public string PickupLocation { get; set; } = string.Empty;
    public string? PickupArea { get; set; }
    public double? PickupLat { get; set; }
    public double? PickupLng { get; set; }

    public string DropoffLocation { get; set; } = string.Empty;
    public string? DropoffArea { get; set; }
    public double? DropoffLat { get; set; }
    public double? DropoffLng { get; set; }

    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;

    public string CargoDescription { get; set; } = string.Empty;
    public string? ParcelType { get; set; }
    public decimal EstimatedWeight { get; set; }
    public string? ParcelSize { get; set; }
    public int ParcelCount { get; set; } = 1;

    public DateOnly? DeliveryDate { get; set; }
    public TimeOnly? DeliveryTimeFrom { get; set; }
    public TimeOnly? DeliveryTimeTo { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }

    public ShipmentRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
