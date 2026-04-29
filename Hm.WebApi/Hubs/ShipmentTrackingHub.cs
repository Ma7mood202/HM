using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using Hm.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Hm.WebApi.Hubs;

/// <summary>
/// SignalR hub for real-time shipment tracking.
/// Clients join/leave groups by shipment ID to receive LocationUpdated and StatusChanged events.
/// Group membership is authorized: caller must be the merchant of the shipment's request,
/// the assigned driver, or the truck account that owns the accepted offer.
/// </summary>
[Authorize]
public class ShipmentTrackingHub : Hub
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentProfileAccessor _profileAccessor;

    public ShipmentTrackingHub(IApplicationDbContext db, ICurrentProfileAccessor profileAccessor)
    {
        _db = db;
        _profileAccessor = profileAccessor;
    }

    /// <summary>Subscribe to real-time updates for a specific shipment.</summary>
    public async Task JoinShipmentTracking(Guid shipmentId)
    {
        // [Authorize] guarantees Context.User is non-null; GetUserId() can still return null if the
        // JWT is missing the sub/NameIdentifier claim — that's what this null-coalesce guards against.
        var userId = Context.User!.GetUserId()
            ?? throw new HubException("User identifier not found.");

        if (!await IsAuthorizedForShipmentAsync(userId, shipmentId))
            throw new HubException("Not authorized to subscribe to this shipment.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"shipment-{shipmentId}");
    }

    /// <summary>Unsubscribe from updates for a specific shipment.</summary>
    public async Task LeaveShipmentTracking(Guid shipmentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"shipment-{shipmentId}");
    }

    private async Task<bool> IsAuthorizedForShipmentAsync(Guid userId, Guid shipmentId)
    {
        var ct = Context.ConnectionAborted;
        var shipment = await _db.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shipmentId, ct);
        if (shipment == null) return false;

        // Driver branch
        var driverProfileId = await _profileAccessor.GetDriverProfileIdAsync(userId, ct);
        if (driverProfileId.HasValue && shipment.DriverProfileId == driverProfileId.Value)
            return true;

        // Truck account branch
        var truckAccountId = await _profileAccessor.GetTruckAccountIdAsync(userId, ct);
        if (truckAccountId.HasValue)
        {
            var offer = await _db.ShipmentOffers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shipment.AcceptedOfferId, ct);
            if (offer != null && offer.TruckAccountId == truckAccountId.Value)
                return true;
        }

        // Merchant branch — resolve the caller's merchant profile id via the accessor (one DB hit)
        // and compare against the shipment-request's MerchantProfileId, avoiding a separate
        // MerchantProfiles lookup.
        var merchantProfileId = await _profileAccessor.GetMerchantProfileIdAsync(userId, ct);
        if (merchantProfileId.HasValue)
        {
            var request = await _db.ShipmentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == shipment.ShipmentRequestId, ct);
            if (request != null && request.MerchantProfileId == merchantProfileId.Value)
                return true;
        }

        return false;
    }
}
