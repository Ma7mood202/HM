namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Request model for uploading national ID.
/// </summary>
public class UploadNationalIdRequest
{
    /// <summary>
    /// Base64 encoded image or URL of the national ID.
    /// </summary>
    public string NationalIdImage { get; set; } = string.Empty;
}
