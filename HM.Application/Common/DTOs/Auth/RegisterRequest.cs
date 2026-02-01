using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Auth;

/// <summary>
/// Request model for user registration.
/// </summary>
public class RegisterRequest
{
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public UserType UserType { get; set; }

    /// <summary>
    /// Optional company name for Merchant registration.
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Optional display name for TruckAccount registration.
    /// </summary>
    public string? DisplayName { get; set; }
}
