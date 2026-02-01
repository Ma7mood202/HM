using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.DTOs.Shipment;
using HM.Application.Common.Models;

namespace HM.Application.Interfaces.Services;

/// <summary>
/// Merchant operations service contract.
/// </summary>
public interface IMerchantService
{
    /// <summary>
    /// Creates a new shipment request.
    /// </summary>
    Task<ShipmentRequestDto> CreateShipmentRequestAsync(Guid merchantProfileId, CreateShipmentRequestDto request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all shipment requests for a merchant.
    /// </summary>
    Task<PaginatedResult<ShipmentRequestDto>> GetMyShipmentRequestsAsync(Guid merchantProfileId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all offers for a specific shipment request.
    /// </summary>
    Task<PaginatedResult<ShipmentOfferDto>> GetShipmentOffersAsync(Guid merchantProfileId, Guid shipmentRequestId, PaginationRequest pagination, CancellationToken cancellationToken = default);

    /// <summary>
    /// Accepts an offer for a shipment request.
    /// </summary>
    Task<ShipmentDetailsDto> AcceptOfferAsync(Guid merchantProfileId, Guid offerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets tracking information for a shipment.
    /// </summary>
    Task<ShipmentTrackingDto> GetShipmentTrackingAsync(Guid merchantProfileId, Guid shipmentId, CancellationToken cancellationToken = default);
}
