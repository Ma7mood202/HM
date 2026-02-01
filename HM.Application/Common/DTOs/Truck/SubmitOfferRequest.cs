namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Request model for submitting an offer on a shipment request.
/// </summary>
public class SubmitOfferRequest
{
    public Guid ShipmentRequestId { get; set; }
    public decimal Price { get; set; }
    public string? Notes { get; set; }
    public DateTime? ExpirationAt { get; set; }
}
