namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Request model for requesting password reset OTP.
/// </summary>
public class ForgotPasswordRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}
