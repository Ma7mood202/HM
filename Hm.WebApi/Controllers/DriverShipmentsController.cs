using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hm.WebApi.Controllers;

[ApiController]
[Route("api/driver/shipments")]
[Authorize(Roles = nameof(UserType.Driver))]
public class DriverShipmentsController : ControllerBase
{
    private readonly IDriverService _driverService;

    public DriverShipmentsController(IDriverService driverService)
    {
        _driverService = driverService;
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

    [HttpPost("{shipmentId:guid}/start")]
    public async Task<IActionResult> StartTrip(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.StartTripAsync(userId, shipmentId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{shipmentId:guid}/arrive")]
    public async Task<IActionResult> MarkArrived(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.MarkArrivedAsync(userId, shipmentId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{shipmentId:guid}/received")]
    public async Task<IActionResult> MarkReceived(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.MarkReceivedAsync(userId, shipmentId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{shipmentId:guid}/tracking")]
    public async Task<IActionResult> GetTracking(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.GetMyShipmentTrackingAsync(userId, shipmentId, cancellationToken);
        return Ok(result);
    }
}
