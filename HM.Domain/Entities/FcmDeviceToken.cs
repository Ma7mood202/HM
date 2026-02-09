namespace HM.Domain.Entities;

/// <summary>
/// FCM device registration token for sending push notifications to a user's device.
/// </summary>
public class FcmDeviceToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty; // e.g. "android", "ios", "web"
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public User User { get; set; } = null!;
}
