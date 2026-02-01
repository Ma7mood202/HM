using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Response model for truck data.
/// </summary>
public class TruckDto
{
    public Guid Id { get; set; }
    public Guid TruckAccountId { get; set; }
    public TruckType TruckType { get; set; }
    public decimal MaxWeight { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
