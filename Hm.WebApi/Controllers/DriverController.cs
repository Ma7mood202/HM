using HM.Application.Common.DTOs.Driver;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
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

    public DriverController(IDriverService driverService, ICurrentProfileAccessor profileAccessor)
    {
        _driverService = driverService;
        _profileAccessor = profileAccessor;
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

    [HttpPost("id")]
    public async Task<IActionResult> UploadNationalId([FromBody] UploadNationalIdRequest request, CancellationToken cancellationToken)
    {
        var driverProfileId = await GetDriverProfileIdAsync(cancellationToken);
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
