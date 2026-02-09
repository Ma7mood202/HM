namespace HM.Domain.Entities;

public class MerchantProfile
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}
