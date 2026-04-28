namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Response after a driver GPS location update is accepted.
/// Also used as the SignalR "LocationUpdated" event payload.
/// </summary>
public class LocationUpdateResponse
{
    public Guid ShipmentId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime UpdatedAt { get; set; }
}
