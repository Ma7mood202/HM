using HM.Application.Common.DTOs.Driver;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Hm.WebApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hm.WebApi.Controllers;

[ApiController]
[Route("api/driver")]
[Authorize(Roles = nameof(UserType.Driver))]
public class DriverController : ControllerBase
{
    private readonly IDriverService _driverService;
    private readonly ICurrentProfileAccessor _profileAccessor;
    private readonly IFileUploadService _fileUpload;

    public DriverController(IDriverService driverService, ICurrentProfileAccessor profileAccessor, IFileUploadService fileUpload)
    {
        _driverService = driverService;
        _profileAccessor = profileAccessor;
        _fileUpload = fileUpload;
    }

    private async Task<Guid> GetDriverProfileIdAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null)
            throw new UnauthorizedAccessException("User identifier not found.");
        var profileId = await _profileAccessor.GetDriverProfileIdAsync(userId.Value, cancellationToken);
        if (profileId == null)
            throw new UnauthorizedAccessException("Driver profile not found.");
        return profileId.Value;
    }

    /// <summary>Get current driver profile (full name, phone, avatar, national ID front/back).</summary>
    [HttpGet("profile")]
    public async Task<ActionResult<DriverProfileDto>> GetProfile(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var result = await _driverService.GetMyProfileAsync(userId.Value, cancellationToken);
        return Ok(result);
    }

    /// <summary>Update profile (full name, phone, avatar, national ID front/back). Use form-data; send only fields to change. Files: Avatar, NationalIdFrontImage, NationalIdBackImage.</summary>
    [HttpPut("profile")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<DriverProfileDto>> UpdateProfile([FromForm] string? FullName, [FromForm] string? PhoneNumber, [FromForm] IFormFile? Avatar, [FromForm] IFormFile? NationalIdFrontImage, [FromForm] IFormFile? NationalIdBackImage, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();

        string? avatarUrl = null, frontUrl = null, backUrl = null;
        if (Avatar != null)
            avatarUrl = await _fileUpload.SaveImageAsync(Avatar, "driver-avatars", cancellationToken);
        if (NationalIdFrontImage != null)
            frontUrl = await _fileUpload.SaveImageAsync(NationalIdFrontImage, "driver-national-id", cancellationToken);
        if (NationalIdBackImage != null)
            backUrl = await _fileUpload.SaveImageAsync(NationalIdBackImage, "driver-national-id", cancellationToken);

        var request = new UpdateDriverProfileRequest
        {
            FullName = string.IsNullOrWhiteSpace(FullName) ? null : FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(PhoneNumber) ? null : PhoneNumber.Trim(),
            AvatarUrl = avatarUrl,
            NationalIdFrontImageUrl = frontUrl,
            NationalIdBackImageUrl = backUrl
        };
        var result = await _driverService.UpdateMyProfileAsync(userId.Value, request, cancellationToken);
        return Ok(result);
    }

    /// <summary>Upload national ID (front and back required) as form-data files.</summary>
    [HttpPost("id")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadNationalId([FromForm] IFormFile? NationalIdFrontImage, [FromForm] IFormFile? NationalIdBackImage, CancellationToken cancellationToken)
    {
        if (NationalIdFrontImage == null || NationalIdFrontImage.Length == 0)
            return BadRequest("National ID front image is required.");
        if (NationalIdBackImage == null || NationalIdBackImage.Length == 0)
            return BadRequest("National ID back image is required.");
        var driverProfileId = await GetDriverProfileIdAsync(cancellationToken);
        var frontUrl = await _fileUpload.SaveImageAsync(NationalIdFrontImage, "driver-national-id", cancellationToken);
        var backUrl = await _fileUpload.SaveImageAsync(NationalIdBackImage, "driver-national-id", cancellationToken);
        var request = new UploadNationalIdRequest { NationalIdFrontImageUrl = frontUrl, NationalIdBackImageUrl = backUrl };
        var result = await _driverService.UploadNationalIdAsync(driverProfileId, request, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipments/{id}/start")]
    public async Task<IActionResult> StartShipment(Guid id, CancellationToken cancellationToken)
    {
        var driverProfileId = await GetDriverProfileIdAsync(cancellationToken);
        var result = await _driverService.StartShipmentAsync(driverProfileId, id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipments/{id}/pause")]
    public async Task<IActionResult> PauseShipment(Guid id, CancellationToken cancellationToken)
    {
        var driverProfileId = await GetDriverProfileIdAsync(cancellationToken);
        var result = await _driverService.PauseShipmentAsync(driverProfileId, id, cancellationToken);
        return Ok(result);
    }

    [HttpPost("shipments/{id}/complete")]
    public async Task<IActionResult> CompleteShipment(Guid id, CancellationToken cancellationToken)
    {
        var driverProfileId = await GetDriverProfileIdAsync(cancellationToken);
        var result = await _driverService.CompleteShipmentAsync(driverProfileId, id, cancellationToken);
        return Ok(result);
    }

    [HttpGet("shipments/{id}")]
    public async Task<IActionResult> GetShipment(Guid id, CancellationToken cancellationToken)
    {
        var driverProfileId = await GetDriverProfileIdAsync(cancellationToken);
        var result = await _driverService.GetAssignedShipmentAsync(driverProfileId, id, cancellationToken);
        return Ok(result);
    }
}
