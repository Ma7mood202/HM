using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserType UserType { get; set; }
    public bool IsActive { get; set; }
    public bool IsOtpVerified { get; set; }
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public OtpPurpose OtpPurpose { get; set; }
    public DateTime CreatedAt { get; set; }
}
