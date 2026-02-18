using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Request to add a truck to the current truck account.
/// </summary>
public class CreateTruckRequest
{
    public TruckType TruckType { get; set; }
    public decimal MaxWeight { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
}
