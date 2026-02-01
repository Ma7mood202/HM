using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>
/// Request model for creating a new shipment request.
/// </summary>
public class CreateShipmentRequestDto
{
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string CargoDescription { get; set; } = string.Empty;
    public TruckType RequiredTruckType { get; set; }
    public decimal EstimatedWeight { get; set; }
    public string? Notes { get; set; }
}
