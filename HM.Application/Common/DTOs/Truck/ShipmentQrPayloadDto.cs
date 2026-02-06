namespace HM.Application.Common.DTOs.Truck;

/// <summary>
/// QR code payload for shipment tracking (frontend renders QR image).
/// </summary>
public class ShipmentQrPayloadDto
{
    public string Payload { get; set; } = string.Empty;
}
