namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Request model for OTP verification (after registration or login when RequiresOtpVerification is true).
/// </summary>
public class VerifyOtpRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
}
