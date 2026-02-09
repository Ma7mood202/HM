namespace Hm.WebApi.Services;

public sealed class FileUploadService : IFileUploadService
{
    private const long MaxImageSizeBytes = 5 * 1024 * 1024; // 5MB
    private static readonly HashSet<string> AllowedImageContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"
    };
    private static readonly Dictionary<string, string> ContentTypeToExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/jpg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp"
    };

    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileUploadService> _logger;

    public FileUploadService(IWebHostEnvironment env, ILogger<FileUploadService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string> SaveImageAsync(IFormFile file, string category, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ArgumentException("No file or empty file.", nameof(file));

        if (file.Length > MaxImageSizeBytes)
            throw new InvalidOperationException($"File size must not exceed {MaxImageSizeBytes / (1024 * 1024)}MB.");

        var contentType = file.ContentType ?? "";
        if (!AllowedImageContentTypes.Contains(contentType))
            throw new InvalidOperationException("Only image files are allowed (JPEG, PNG, GIF, WebP).");

        var ext = ContentTypeToExtension.GetValueOrDefault(contentType) ?? ".jpg";
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var uploadsDir = Path.Combine(_env.ContentRootPath, "uploads", category);
        Directory.CreateDirectory(uploadsDir);
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            await file.CopyToAsync(stream, cancellationToken);
        }

        var relativePath = $"/uploads/{category}/{fileName}";
        _logger.LogDebug("Saved upload: {Path}", relativePath);
        return relativePath;
    }
}
