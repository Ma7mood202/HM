using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class ShipmentRequest
{
    public Guid Id { get; set; }
    public Guid MerchantProfileId { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public TruckType RequiredTruckType { get; set; }
    public decimal EstimatedWeight { get; set; }
    public string? Notes { get; set; }
    public ShipmentRequestStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
