namespace HM.Application.Common.DTOs.Notification;

/// <summary>
/// Request to register an FCM device token for the current user.
/// </summary>
public class RegisterDeviceRequest
{
    public string Token { get; set; } = string.Empty;
    /// <summary>Platform: "android", "ios", or "web".</summary>
    public string Platform { get; set; } = string.Empty;
}
