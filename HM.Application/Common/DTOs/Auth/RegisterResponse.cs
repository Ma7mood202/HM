namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Response model for user registration (signup).
/// </summary>
public class RegisterResponse
{
    /// <summary>
    /// Whether the registration completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Human-readable message (e.g. "Registration successful. Please verify your phone with the OTP sent.")
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When true, the client must call verify-otp before the user can log in and receive a token.
    /// </summary>
    public bool RequiresOtpVerification { get; set; }

    /// <summary>
    /// The created user's ID. Optional; useful for client tracking.
    /// </summary>
    public Guid? UserId { get; set; }
}
