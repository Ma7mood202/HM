namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Request model for resending OTP.
/// </summary>
public class ResendOtpRequest
{
    public string PhoneNumber { get; set; } = string.Empty;
}
