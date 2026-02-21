using HM.Application.Common.DTOs.Truck;
using HM.Application.Common.Models;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Hm.WebApi.Services;
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
    private readonly IFileUploadService _fileUpload;

    public TruckController(ITruckService truckService, ICurrentProfileAccessor profileAccessor, IFileUploadService fileUpload)
    {
        _truckService = truckService;
        _profileAccessor = profileAccessor;
        _fileUpload = fileUpload;
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

    /// <summary>Get current truck account profile (full name, phone, avatar, national ID front/back).</summary>
    [HttpGet("profile")]
    public async Task<ActionResult<TruckProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _truckService.GetMyProfileAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>Update profile (full name, phone, avatar, national ID front/back). Use form-data; send only fields to change.</summary>
    [HttpPut("profile")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<TruckProfileDto>> UpdateProfile([FromForm] UpdateTruckProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        if (request.Avatar != null)
            request.AvatarUrl = await _fileUpload.SaveImageAsync(request.Avatar, "truck-avatars", cancellationToken);
        if (request.NationalIdFrontImage != null)
            request.NationalIdFrontImageUrl = await _fileUpload.SaveImageAsync(request.NationalIdFrontImage, "truck-national-id", cancellationToken);
        if (request.NationalIdBackImage != null)
            request.NationalIdBackImageUrl = await _fileUpload.SaveImageAsync(request.NationalIdBackImage, "truck-national-id", cancellationToken);

        var result = await _truckService.UpdateMyProfileAsync(userId.Value, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Upload national ID (front and back required) as form-data.</summary>
    [HttpPost("id")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadNationalId([FromForm] UploadTruckNationalIdRequest request, CancellationToken cancellationToken)
    {
        if (request.NationalIdFrontImage == null || request.NationalIdFrontImage.Length == 0)
            return BadRequest("National ID front image is required.");
        if (request.NationalIdBackImage == null || request.NationalIdBackImage.Length == 0)
            return BadRequest("National ID back image is required.");
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        request.NationalIdFrontImageUrl = await _fileUpload.SaveImageAsync(request.NationalIdFrontImage, "truck-national-id", cancellationToken);
        request.NationalIdBackImageUrl = await _fileUpload.SaveImageAsync(request.NationalIdBackImage, "truck-national-id", cancellationToken);
        var result = await _truckService.UploadNationalIdAsync(truckAccountId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get all available (open) shipment requests; optional filter via query (TruckType, MinWeight, MaxWeight, FromRegion, ToRegion).</summary>
    [HttpGet("shipments/open")]
    public async Task<IActionResult> GetOpenShipmentRequests([FromQuery] ShipmentRequestFilterDto? filter, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _truckService.GetOpenShipmentRequestsAsync(filter, pagination, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get full details of a shipment request (no merchant contact).</summary>
    [HttpGet("requests/{id:guid}")]
    public async Task<IActionResult> GetShipmentRequestDetails(Guid id, CancellationToken cancellationToken)
    {
        var result = await _truckService.GetShipmentRequestDetailsAsync(id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Add a truck (required before submitting offers). New trucks are active by default.</summary>
    [HttpPost("trucks")]
    public async Task<IActionResult> CreateTruck([FromBody] CreateTruckRequest request, CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.CreateTruckAsync(truckAccountId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>List all trucks for the current truck account.</summary>
    [HttpGet("trucks")]
    public async Task<IActionResult> GetMyTrucks(CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.GetMyTrucksAsync(truckAccountId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("offers")]
    public async Task<IActionResult> SubmitOffer([FromBody] SubmitOfferRequest request, CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.SubmitOfferAsync(truckAccountId, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get offers submitted by the current truck account.</summary>
    [HttpGet("offers")]
    public async Task<IActionResult> GetMyOffers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _truckService.GetMyOffersAsync(truckAccountId, pagination, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipments/{id:guid}/assign-self")]
    public async Task<IActionResult> AssignSelfAsDriver(Guid id, [FromBody] AssignSelfRequest request, CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.AssignSelfAsDriverAsync(truckAccountId, id, request.TruckId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipments/{id:guid}/invite-driver")]
    public async Task<IActionResult> InviteDriver(Guid id, CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.GenerateDriverInvitationAsync(truckAccountId, id, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get shipments assigned to the current truck account.</summary>
    [HttpGet("shipments")]
    public async Task<IActionResult> GetMyShipments([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _truckService.GetMyShipmentsAsync(truckAccountId, pagination, cancellationToken);
        return Ok(result);
    }

    /// <summary>Get QR payload (ShipmentId + tracking URL) for shipment tracking.</summary>
    [HttpGet("shipments/{id:guid}/qr")]
    public async Task<IActionResult> GetShipmentQr(Guid id, CancellationToken cancellationToken)
    {
        var truckAccountId = await GetTruckAccountIdAsync(cancellationToken);
        var result = await _truckService.GetShipmentQrPayloadAsync(truckAccountId, id, cancellationToken);
        return Ok(result);
    }
}
