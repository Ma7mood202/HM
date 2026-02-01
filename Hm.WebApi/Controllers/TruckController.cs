using HM.Application.Common.DTOs.Truck;
using HM.Application.Common.Models;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hm.WebApi.Controllers;

[ApiController]
[Route("api/truck")]
[Authorize(Roles = nameof(UserType.TruckAccount))]
public class TruckController : ControllerBase
{
    private readonly ITruckService _truckService;
    private readonly ICurrentProfileAccessor _profileAccessor;

    public TruckController(ITruckService truckService, ICurrentProfileAccessor profileAccessor)
    {
        _truckService = truckService;
        _profileAccessor = profileAccessor;
    }

    private async Task<Guid> GetTruckAccountIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null)
            throw new UnauthorizedAccessException("User identifier not found.");
        var accountId = await _profileAccessor.GetTruckAccountIdAsync(userId.Value, cancellationToken);
        if (accountId == null)
            throw new UnauthorizedAccessException("Truck account not found.");
        return accountId.Value;
    }

    [HttpGet("shipments/open")]
    public async Task<IActionResult> GetOpenShipments([FromQuery] ShipmentRequestFilterDto? filter, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _truckService.GetOpenShipmentRequestsAsync(filter, pagination, cancellationToken);
        return Ok(result);
    }

    [HttpPost("offers")]
    public async Task<IActionResult> SubmitOffer([FromBody] SubmitOfferRequest request, CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.SubmitOfferAsync(truckAccountId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipments/{id}/assign-self")]
    public async Task<IActionResult> AssignSelfAsDriver(Guid id, [FromBody] AssignSelfRequest request, CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.AssignSelfAsDriverAsync(truckAccountId, id, request.TruckId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipments/{id}/invite-driver")]
    public async Task<IActionResult> InviteDriver(Guid id, CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.GenerateDriverInvitationAsync(truckAccountId, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipments")]
    public async Task<IActionResult> GetMyShipments([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _truckService.GetMyShipmentsAsync(truckAccountId, pagination, cancellationToken);
        return Ok(result);
    }
}
