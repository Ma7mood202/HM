using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Response model for shipment offer data.
/// PickupLocation/DropoffLocation set when used for truck "my offers" list.
/// </summary>
public class ShipmentOfferDto
{
    public Guid Id { get; set; }
    public Guid ShipmentRequestId { get; set; }
    public Guid TruckAccountId { get; set; }
    public string TruckAccountName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    public ShipmentOfferStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpirationAt { get; set; }
    public string? PickupLocation { get; set; }
    public string? DropoffLocation { get; set; }
}
