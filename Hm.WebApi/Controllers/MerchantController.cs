using HM.Application.Common.DTOs.Merchant;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Hm.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hm.WebApi.Controllers;

[ApiController]
[Route("api/merchant")]
[Authorize(Roles = nameof(UserType.Merchant))]
public class MerchantController : ControllerBase
{
    private readonly IMerchantService _merchantService;
    private readonly IFileUploadService _fileUpload;

    public MerchantController(IMerchantService merchantService, IFileUploadService fileUpload)
    {
        _merchantService = merchantService;
        _fileUpload = fileUpload;
    }

    private Guid GetUserId()
    {
        var userId = User.GetUserId();
        if (userId == null)
            throw new UnauthorizedAccessException("User identifier not found.");
        return userId.Value;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _merchantService.GetMyProfileAsync(userId, cancellationToken);
        return Ok(result);
    }

    /// <summary>Update profile. Use form-data: FullName (required), Avatar (optional file).</summary>
    [HttpPut("profile")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UpdateProfile([FromForm] UpdateMerchantProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (request.Avatar != null)
            request.AvatarUrl = await _fileUpload.SaveImageAsync(request.Avatar, "merchant-avatars", cancellationToken);
        var result = await _merchantService.UpdateMyProfileAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipment-requests")]
    public async Task<IActionResult> CreateShipmentRequest([FromBody] CreateShipmentRequestRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _merchantService.CreateShipmentRequestAsync(userId, request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipment-requests")]
    public async Task<IActionResult> GetShipmentRequests(
        [FromQuery] ShipmentRequestStatus? status,
        [FromQuery] string? search,
        [FromQuery] DateOnly? dateFrom,
        [FromQuery] DateOnly? dateTo,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var query = new GetMerchantShipmentRequestsQuery
        {
            Status = status,
            Search = search,
            DateFrom = dateFrom,
            DateTo = dateTo,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
        var result = await _merchantService.GetMyShipmentRequestsAsync(userId, query, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipment-requests/{id:guid}")]
    public async Task<IActionResult> GetShipmentRequestDetails(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _merchantService.GetMyShipmentRequestDetailsAsync(userId, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipment-requests/{id:guid}/offers")]
    public async Task<IActionResult> GetOffers(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _merchantService.GetOffersForMyRequestAsync(userId, id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipment-requests/{id:guid}/offers/{offerId:guid}/accept")]
    public async Task<IActionResult> AcceptOffer(Guid id, Guid offerId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _merchantService.AcceptOfferAsync(userId, id, offerId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipment-requests/{id:guid}/cancel")]
    public async Task<IActionResult> CancelShipmentRequest(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        await _merchantService.CancelShipmentRequestAsync(userId, id, cancellationToken);
        return NoContent();
    }

    [HttpGet("shipments/{shipmentId:guid}/tracking")]
    public async Task<IActionResult> GetTracking(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _merchantService.GetTrackingAsync(userId, shipmentId, cancellationToken);
        return Ok(result);
    }
}
