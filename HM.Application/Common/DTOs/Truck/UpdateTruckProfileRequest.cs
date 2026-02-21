using Microsoft.AspNetCore.Http;

namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Request to update truck account profile (form-data). All fields optional. Controller saves files and sets *Url fields before calling service.
/// </summary>
public class UpdateTruckProfileRequest
{
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? NationalIdFrontImageUrl { get; set; }
    public string? NationalIdBackImageUrl { get; set; }
    public IFormFile? Avatar { get; set; }
    public IFormFile? NationalIdFrontImage { get; set; }
    public IFormFile? NationalIdBackImage { get; set; }
}
