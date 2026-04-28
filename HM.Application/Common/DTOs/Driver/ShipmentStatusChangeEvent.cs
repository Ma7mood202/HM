using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// SignalR "StatusChanged" event payload pushed to tracking group.
/// </summary>
public class ShipmentStatusChangeEvent
{
    public Guid ShipmentId { get; set; }
    public ShipmentStatus Status { get; set; }
    public DateTime ChangedAt { get; set; }
}
