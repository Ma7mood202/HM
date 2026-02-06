namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Merchant profile view for profile screen.
/// </summary>
public class MerchantProfileResponse
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime CreatedAt { get; set; }
}
