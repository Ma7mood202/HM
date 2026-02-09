namespace Hm.WebApi.Services;

/// <summary>
/// Saves uploaded files to the server and returns a URL path that can be used to access them.
/// </summary>
public interface IFileUploadService
{
    /// <summary>
    /// Saves the file under wwwroot/uploads/{category}/ and returns the path (e.g. /uploads/avatars/guid.jpg).
    /// Allowed: image types only, max 5MB. Throws if invalid.
    /// </summary>
    Task<string> SaveImageAsync(IFormFile file, string category, CancellationToken cancellationToken = default);
}
