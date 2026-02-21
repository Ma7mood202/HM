using Microsoft.AspNetCore.Http;

namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Request to upload national ID (form-data). Front and back images required. Controller saves files and sets *Url before calling service.
/// </summary>
public class UploadTruckNationalIdRequest
{
    public IFormFile? NationalIdFrontImage { get; set; }
    public IFormFile? NationalIdBackImage { get; set; }
    public string? NationalIdFrontImageUrl { get; set; }
    public string? NationalIdBackImageUrl { get; set; }
}
