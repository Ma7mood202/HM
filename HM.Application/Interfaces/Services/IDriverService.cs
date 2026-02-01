using HM.Application.Common.DTOs.Driver;
using HM.Application.Common.DTOs.Shipment;

namespace HM.Application.Interfaces.Services;

/// <summary>
/// Driver operations service contract.
/// </summary>
public interface IDriverService
{
    /// <summary>
    /// Uploads the driver's national ID for verification.
    /// </summary>
    Task<DriverProfileDto> UploadNationalIdAsync(Guid driverProfileId, UploadNationalIdRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the shipment trip.
    /// </summary>
    Task<ShipmentStatusDto> StartShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pauses the shipment trip.
    /// </summary>
    Task<ShipmentStatusDto> PauseShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Completes the shipment trip.
    /// </summary>
    Task<ShipmentStatusDto> CompleteShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the assigned shipment details (without price information).
    /// </summary>
    Task<AssignedShipmentDto> GetAssignedShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default);
}
