using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.Models;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hm.WebApi.Controllers;

[ApiController]
[Route("api/merchant")]
[Authorize(Roles = nameof(UserType.Merchant))]
public class MerchantController : ControllerBase
{
    private readonly IMerchantService _merchantService;
    private readonly ICurrentProfileAccessor _profileAccessor;

    public MerchantController(IMerchantService merchantService, ICurrentProfileAccessor profileAccessor)
    {
        _merchantService = merchantService;
        _profileAccessor = profileAccessor;
    }

    private async Task<Guid> GetMerchantProfileIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null)
            throw new UnauthorizedAccessException("User identifier not found.");
        var profileId = await _profileAccessor.GetMerchantProfileIdAsync(userId.Value, cancellationToken);
        if (profileId == null)
            throw new UnauthorizedAccessException("Merchant profile not found.");
        return profileId.Value;
    }

    [HttpPost("shipments")]
    public async Task<IActionResult> CreateShipment([FromBody] CreateShipmentRequestDto request, CancellationToken cancellationToken)
    {
        var merchantProfileId = await GetMerchantProfileIdAsync(cancellationToken);
        var result = await _merchantService.CreateShipmentRequestAsync(merchantProfileId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipments")]
    public async Task<IActionResult> GetShipments([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var merchantProfileId = await GetMerchantProfileIdAsync(cancellationToken);
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _merchantService.GetMyShipmentRequestsAsync(merchantProfileId, pagination, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipments/{id}/offers")]
    public async Task<IActionResult> GetShipmentOffers(Guid id, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var merchantProfileId = await GetMerchantProfileIdAsync(cancellationToken);
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _merchantService.GetShipmentOffersAsync(merchantProfileId, id, pagination, cancellationToken);
        return Ok(result);
    }

    [HttpPost("offers/{offerId}/accept")]
    public async Task<IActionResult> AcceptOffer(Guid offerId, CancellationToken cancellationToken)
    {
        var merchantProfileId = await GetMerchantProfileIdAsync(cancellationToken);
        var result = await _merchantService.AcceptOfferAsync(merchantProfileId, offerId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipments/{id}/tracking")]
    public async Task<IActionResult> GetTracking(Guid id, CancellationToken cancellationToken)
    {
        var merchantProfileId = await GetMerchantProfileIdAsync(cancellationToken);
        var result = await _merchantService.GetShipmentTrackingAsync(merchantProfileId, id, cancellationToken);
        return Ok(result);
    }
}
