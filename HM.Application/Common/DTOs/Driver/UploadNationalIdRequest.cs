namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Result of saving national ID files (URLs). Used by controller after file upload.
/// </summary>
public class UploadNationalIdRequest
{
    public string? NationalIdFrontImageUrl { get; set; }
    public string? NationalIdBackImageUrl { get; set; }
}
