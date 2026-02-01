namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// Request body for assigning the truck account owner as driver (truckId required).
/// </summary>
public class AssignSelfRequest
{
    public Guid TruckId { get; set; }
}
