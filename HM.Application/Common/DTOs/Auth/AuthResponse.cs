using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Response model for authentication operations.
/// </summary>
public class AuthResponse
{
    public Guid UserId { get; set; }
    public UserType UserType { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    /// <summary>
    /// When true, client must call verify-otp before receiving a token.
    /// </summary>
    public bool RequiresOtpVerification { get; set; }
}
