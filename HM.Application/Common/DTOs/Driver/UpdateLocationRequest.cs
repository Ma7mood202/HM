namespace HM.Application.Common.DTOs.Driver;

/// <summary>
/// Request payload for driver GPS location update.
/// </summary>
public class UpdateLocationRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
