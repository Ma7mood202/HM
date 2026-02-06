namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Request to update merchant profile.
/// </summary>
public class UpdateMerchantProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
}
