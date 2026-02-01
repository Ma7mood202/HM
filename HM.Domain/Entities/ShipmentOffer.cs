using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class ShipmentOffer
{
    public Guid Id { get; set; }
    public Guid ShipmentRequestId { get; set; }
    public Guid TruckAccountId { get; set; }
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    public ShipmentOfferStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpirationAt { get; set; }
}
