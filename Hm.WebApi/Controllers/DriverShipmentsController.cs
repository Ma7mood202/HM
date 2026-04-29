using HM.Application.Common.DTOs.Driver;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Hm.WebApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Hm.WebApi.Controllers;

[ApiController]
[Route("api/driver/shipments")]
[Authorize(Roles = nameof(UserType.Driver))]
public class DriverShipmentsController : ControllerBase
{
    private readonly IDriverService _driverService;
    private readonly IHubContext<ShipmentTrackingHub> _hubContext;

    public DriverShipmentsController(IDriverService driverService, IHubContext<ShipmentTrackingHub> hubContext)
    {
        _driverService = driverService;
        _hubContext = hubContext;
    }

    private Guid GetUserId()
    {
        var userId = User.GetUserId();
        if (userId == null)
            throw new UnauthorizedAccessException("User identifier not found.");
        return userId.Value;
    }

    [HttpGet("{shipmentId:guid}")]
    public async Task<IActionResult> GetShipmentDetails(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.GetMyShipmentDetailsAsync(userId, shipmentId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Driver accepts an assignment in PendingDriverAcceptance. Transitions to Ready.</summary>
    [HttpPost("{shipmentId:guid}/accept")]
    public async Task<IActionResult> AcceptAssignment(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.AcceptAssignmentAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }

    /// <summary>Driver rejects an assignment in PendingDriverAcceptance. Reverts to AwaitingDriver.</summary>
    [HttpPost("{shipmentId:guid}/reject")]
    public async Task<IActionResult> RejectAssignment(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.RejectAssignmentAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }

    [HttpPost("{shipmentId:guid}/start")]
    public async Task<IActionResult> StartTrip(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.StartTripAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }

    [HttpPost("{shipmentId:guid}/arrive")]
    public async Task<IActionResult> MarkArrived(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.MarkArrivedAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }

    [HttpPost("{shipmentId:guid}/received")]
    public async Task<IActionResult> MarkReceived(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.MarkReceivedAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{shipmentId:guid}/tracking")]
    public async Task<IActionResult> GetTracking(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.GetMyShipmentTrackingAsync(userId, shipmentId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{shipmentId:guid}/update-location")]
    public async Task<IActionResult> UpdateLocation(Guid shipmentId, [FromBody] UpdateLocationRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.UpdateLocationAsync(userId, shipmentId, request, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("LocationUpdated", result, cancellationToken);

        return Ok(result);
    }

    [HttpPost("{shipmentId:guid}/pause")]
    public async Task<IActionResult> PauseTrip(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.PauseTripAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }

    [HttpPost("{shipmentId:guid}/resume")]
    public async Task<IActionResult> ResumeTrip(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.ResumeTripAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }
}
