using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class ShipmentRequest
{
    public Guid Id { get; set; }
    public Guid MerchantProfileId { get; set; }
    /// <summary>Public reference e.g. "HM474936". Generated in Infrastructure.</summary>
    public string RequestNumber { get; set; } = string.Empty;

    public TruckType RequiredTruckType { get; set; }
    public TruckBodyType? RequiredTruckBodyType { get; set; }

    public Guid? PickupGovernorateId { get; set; }
    public Guid? PickupRegionId { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string? PickupArea { get; set; }

    public Guid? DropoffGovernorateId { get; set; }
    public Guid? DropoffRegionId { get; set; }
    public string DropoffLocation { get; set; } = string.Empty;
    public string? DropoffArea { get; set; }

    public string SenderName { get; set; } = string.Empty;
    public string SenderPhone { get; set; } = string.Empty;
    public string? ReceiverPhoneNumber { get; set; }

    public string CargoDescription { get; set; } = string.Empty;
    public string? ParcelType { get; set; }
    /// <summary>Weight in tons.</summary>
    public decimal EstimatedWeightTon { get; set; }
    public string? ParcelSize { get; set; }
    public int ParcelCount { get; set; } = 1;
    public bool IsFragile { get; set; }

    public DateOnly? DeliveryDate { get; set; }
    public TimeOnly? DeliveryTimeFrom { get; set; }
    public TimeOnly? DeliveryTimeTo { get; set; }

    public PaymentMethod PaymentMethod { get; set; }
    public string? Notes { get; set; }

    public ShipmentRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }

    public Governorate? PickupGovernorate { get; set; }
    public Region? PickupRegion { get; set; }
    public Governorate? DropoffGovernorate { get; set; }
    public Region? DropoffRegion { get; set; }
}
