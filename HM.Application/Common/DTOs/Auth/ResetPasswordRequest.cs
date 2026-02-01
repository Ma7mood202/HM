namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Request model for resetting password using OTP.
/// </summary>
public class ResetPasswordRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}
