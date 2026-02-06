using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.DTOs.Shipment;
using HM.Application.Common.DTOs.Truck;
using HM.Application.Common.Models;

namespace HM.Application.Interfaces.Services;

/// <summary>
/// Truck account operations service contract.
/// </summary>
public interface ITruckService
{
    /// <summary>
    /// Gets all open shipment requests available for offers (home screen).
    /// </summary>
    Task<PaginatedResult<ShipmentListItemDto>> GetOpenShipmentRequestsAsync(ShipmentRequestFilterDto? filter, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets full details of a shipment request (no merchant contact).
    /// </summary>
    Task<ShipmentDetailsDto> GetShipmentRequestDetailsAsync(Guid shipmentRequestId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an offer for a shipment request (one offer per truck per request).
    /// </summary>
    Task<ShipmentOfferDto> SubmitOfferAsync(Guid truckAccountId, SubmitOfferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets offers submitted by the current truck account.
    /// </summary>
    Task<PaginatedResult<ShipmentOfferDto>> GetMyOffersAsync(Guid truckAccountId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns the truck account owner as the driver for a shipment (after offer accepted).
    /// </summary>
    Task<ShipmentDetailsDto> AssignSelfAsDriverAsync(Guid truckAccountId, Guid shipmentId, Guid truckId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an invitation link for an external driver (one invitation per shipment).
    /// </summary>
    Task<DriverInvitationDto> GenerateDriverInvitationAsync(Guid truckAccountId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all shipments assigned to the truck account.
    /// </summary>
    Task<PaginatedResult<ShipmentListItemDto>> GetMyShipmentsAsync(Guid truckAccountId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates QR payload (ShipmentId + tracking URL) for shipment tracking.
    /// </summary>
    Task<ShipmentQrPayloadDto> GetShipmentQrPayloadAsync(Guid truckAccountId, Guid shipmentId, CancellationToken cancellationToken = default);
}
