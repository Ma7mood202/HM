using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class Truck
{
    public Guid Id { get; set; }
    public Guid TruckAccountId { get; set; }
    public TruckType TruckType { get; set; }
    public decimal MaxWeight { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
