namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Request to update driver profile (full name, phone, avatar URL, national ID front/back URLs). All fields optional.
/// Controllers set URL fields after saving uploaded files.
/// </summary>
public class UpdateDriverProfileRequest
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? NationalIdFrontImageUrl { get; set; }
    public string? NationalIdBackImageUrl { get; set; }
}
