namespace HM.Domain.Entities;

public class DriverProfile
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public string? NationalIdFrontImageUrl { get; set; }
    public string? NationalIdBackImageUrl { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
