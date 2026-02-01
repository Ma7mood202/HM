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
    /// Gets all open shipment requests available for offers.
    /// </summary>
    Task<PaginatedResult<ShipmentRequestDto>> GetOpenShipmentRequestsAsync(ShipmentRequestFilterDto? filter, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Submits an offer for a shipment request.
    /// </summary>
    Task<ShipmentOfferDto> SubmitOfferAsync(Guid truckAccountId, SubmitOfferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Assigns the truck account owner as the driver for a shipment.
    /// </summary>
    Task<ShipmentDetailsDto> AssignSelfAsDriverAsync(Guid truckAccountId, Guid shipmentId, Guid truckId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an invitation link for an external driver.
    /// </summary>
    Task<DriverInvitationDto> GenerateDriverInvitationAsync(Guid truckAccountId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all shipments assigned to the truck account.
    /// </summary>
    Task<PaginatedResult<ShipmentListItemDto>> GetMyShipmentsAsync(Guid truckAccountId, PaginationRequest pagination, CancellationToken cancellationToken = default);
}
