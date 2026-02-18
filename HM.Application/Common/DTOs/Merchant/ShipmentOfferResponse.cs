using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Offer row for "مشاهدة العروض".
/// </summary>
public class ShipmentOfferResponse
{
    public Guid OfferId { get; set; }
    public Guid TruckAccountId { get; set; }
    public decimal Price { get; set; }
    public string? Currency { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }

    public string TruckAccountName { get; set; } = string.Empty;
    public decimal? Rating { get; set; }
    public int? TrucksCount { get; set; }
    public string? DriverName { get; set; }
    /// <summary>Pending = can be accepted; Accepted/Rejected/Expired = for display only.</summary>
    public ShipmentOfferStatus Status { get; set; }
}
