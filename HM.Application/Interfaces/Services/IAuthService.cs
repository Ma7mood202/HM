using HM.Application.Common.DTOs.Auth;
using HM.Application.Common.Models;

namespace HM.Application.Interfaces.Services;

/// <summary>
/// Authentication and registration service contract.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Changes password for the authenticated user (current password required).
    /// </summary>
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken = default);
    /// <summary>
    /// Registers a new user and generates OTP (expires in 5 minutes). OTP verification required before login.
    /// For driver: pass optional national ID image URLs (from file upload).
    /// </summary>
    Task<RegisterResponse> RegisterAsync(RegisterRequest request, string? nationalIdFrontImageUrl = null, string? nationalIdBackImageUrl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Authenticates a user. Returns RequiresOtpVerification if OTP not yet verified; otherwise returns token.
    /// </summary>
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies OTP after registration or login when RequiresOtpVerification is true. Returns token on success.
    /// </summary>
    Task<AuthResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resends OTP for verification (generates new OTP, 5 minute expiry).
    /// </summary>
    Task<MessageResponse> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests password reset OTP (generates OTP, 5 minute expiry).
    /// </summary>
    Task<MessageResponse> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets password using OTP from forgot-password.
    /// </summary>
    Task<MessageResponse> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Activates a driver account via invitation token.
    /// Pass optional national ID image URLs (from file upload) for driver.
    /// </summary>
    Task<AuthResponse> AcceptDriverInvitationAsync(string token, RegisterRequest request, string? nationalIdFrontImageUrl = null, string? nationalIdBackImageUrl = null, CancellationToken cancellationToken = default);
}
