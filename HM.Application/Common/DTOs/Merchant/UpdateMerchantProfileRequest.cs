using Microsoft.AspNetCore.Http;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Request to update merchant profile (form-data). Controller saves Avatar and sets AvatarUrl before calling service.
/// </summary>
public class UpdateMerchantProfileRequest
{
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public IFormFile? Avatar { get; set; }
}
