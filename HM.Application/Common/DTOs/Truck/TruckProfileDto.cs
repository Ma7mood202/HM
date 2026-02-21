namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Response model for truck account profile (get profile, update response).
/// </summary>
public class TruckProfileDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public string? NationalIdFrontImageUrl { get; set; }
    public string? NationalIdBackImageUrl { get; set; }
    public bool HasNationalId { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
