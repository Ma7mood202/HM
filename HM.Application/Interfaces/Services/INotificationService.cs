using HM.Application.Common.DTOs.Notification;

namespace HM.Application.Interfaces.Services;

public interface INotificationService
{
    Task<IReadOnlyList<NotificationDto>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnseenCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<NotificationDto?> GetByIdAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAsSeenAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsSeenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, Guid notificationId, CancellationToken cancellationToken = default);
    Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken = default);
    Task RegisterDeviceAsync(Guid userId, RegisterDeviceRequest request, CancellationToken cancellationToken = default);
    /// <summary>Creates an in-app notification and optionally sends it via FCM to the user's devices.</summary>
    Task SendNotificationAsync(Guid userId, string title, string body, string? data = null, bool sendPush = true, CancellationToken cancellationToken = default);
}
