using HM.Application.Common.DTOs.Notification;
using HM.Application.Interfaces.Services;
using Hm.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Hm.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    /// <summary>Get all notifications for the current user (newest first).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<NotificationDto>>> GetAll(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var list = await _notificationService.GetAllAsync(userId.Value, cancellationToken);
        return Ok(list);
    }

    /// <summary>Get count of unseen notifications.</summary>
    [HttpGet("unseen-count")]
    public async Task<ActionResult<UnseenCountResponse>> GetUnseenCount(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var count = await _notificationService.GetUnseenCountAsync(userId.Value, cancellationToken);
        return Ok(new UnseenCountResponse { Count = count });
    }

    /// <summary>Get a single notification by id.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<NotificationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        var dto = await _notificationService.GetByIdAsync(userId.Value, id, cancellationToken);
        if (dto == null) return NotFound();
        return Ok(dto);
    }

    /// <summary>Mark a notification as seen.</summary>
    [HttpPost("{id:guid}/mark-seen")]
    public async Task<IActionResult> MarkAsSeen(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        await _notificationService.MarkAsSeenAsync(userId.Value, id, cancellationToken);
        return NoContent();
    }

    /// <summary>Mark all notifications as seen.</summary>
    [HttpPost("mark-all-seen")]
    public async Task<IActionResult> MarkAllAsSeen(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        await _notificationService.MarkAllAsSeenAsync(userId.Value, cancellationToken);
        return NoContent();
    }

    /// <summary>Delete a single notification.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        await _notificationService.DeleteAsync(userId.Value, id, cancellationToken);
        return NoContent();
    }

    /// <summary>Delete all notifications for the current user.</summary>
    [HttpDelete]
    public async Task<IActionResult> DeleteAll(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        await _notificationService.DeleteAllAsync(userId.Value, cancellationToken);
        return NoContent();
    }

    /// <summary>Register an FCM device token for push notifications.</summary>
    [HttpPost("register-device")]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceRequest request, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId == null) return Unauthorized();
        await _notificationService.RegisterDeviceAsync(userId.Value, request, cancellationToken);
        return NoContent();
    }
}
