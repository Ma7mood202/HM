namespace HM.Application.Common.DTOs.Driver;

/// <summary>Lightweight driver projection used by the truck account when picking a driver to assign.</summary>
public class DriverSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsVerified { get; set; }
    public bool HasNationalId { get; set; }
    public DateTime CreatedAt { get; set; }
}
