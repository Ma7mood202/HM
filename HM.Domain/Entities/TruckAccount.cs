namespace HM.Domain.Entities;

public class TruckAccount
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public bool IsAvailable { get; set; }
    public DateTime CreatedAt { get; set; }
}
