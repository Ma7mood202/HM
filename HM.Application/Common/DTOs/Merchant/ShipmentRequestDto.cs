using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Response model for shipment request data.
/// </summary>
public class ShipmentRequestDto
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
    public int OffersCount { get; set; }
}
