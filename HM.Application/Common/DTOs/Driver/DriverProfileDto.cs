namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Response model for driver profile data.
/// </summary>
public class DriverProfileDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public bool HasNationalId { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
