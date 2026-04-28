using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Hm.WebApi.Hubs;

/// <summary>
/// SignalR hub for real-time shipment tracking.
/// Clients join/leave groups by shipment ID to receive LocationUpdated and StatusChanged events.
/// </summary>
[Authorize]
public class ShipmentTrackingHub : Hub
{
    /// <summary>
    /// Subscribe to real-time updates for a specific shipment.
    /// </summary>
    public async Task JoinShipmentTracking(Guid shipmentId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"shipment-{shipmentId}");
    }

    /// <summary>
    /// Unsubscribe from updates for a specific shipment.
    /// </summary>
    public async Task LeaveShipmentTracking(Guid shipmentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"shipment-{shipmentId}");
    }
}
