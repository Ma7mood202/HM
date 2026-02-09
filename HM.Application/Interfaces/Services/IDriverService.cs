using HM.Application.Common.DTOs.Driver;
using HM.Application.Common.DTOs.Shipment;

namespace HM.Application.Interfaces.Services;

/// <summary>
/// Driver operations service contract.
/// </summary>
public interface IDriverService
{
    /// <summary>
    /// Gets the current driver's profile (including phone from user).
    /// </summary>
    Task<DriverProfileDto> GetMyProfileAsync(Guid driverUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates the driver's profile (full name, phone, avatar, national ID front/back). Only provided fields are updated.
    /// </summary>
    Task<DriverProfileDto> UpdateMyProfileAsync(Guid driverUserId, UpdateDriverProfileRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Uploads the driver's national ID (front and back) for verification.
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

    /// <summary>
    /// Gets full shipment details for the driver details screen (by current user id).
    /// </summary>
    Task<DriverShipmentDetailsResponse> GetMyShipmentDetailsAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Start trip ("بدأ الرحلة"). Allowed when AwaitingDriver or Ready → InTransit.
    /// </summary>
    Task<DriverShipmentDetailsResponse> StartTripAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark arrived ("تم الوصول"). Allowed when InTransit → Arrived.
    /// </summary>
    Task<DriverShipmentDetailsResponse> MarkArrivedAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark received/completed ("استلامة"). Allowed when Arrived → Completed.
    /// </summary>
    Task<DriverShipmentDetailsResponse> MarkReceivedAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets minimal tracking data for the shipment (location if stored).
    /// </summary>
    Task<DriverShipmentTrackingResponse> GetMyShipmentTrackingAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default);
}
