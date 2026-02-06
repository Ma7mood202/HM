namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Request to change password (authenticated user).
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
