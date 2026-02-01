using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Shipment;

/// <summary>
/// Response model for shipment status updates.
/// </summary>
public class ShipmentStatusDto
{
    public Guid ShipmentId { get; set; }
    public ShipmentStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
