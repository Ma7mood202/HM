using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.Models;

namespace HM.Application.Interfaces.Services;

/// <summary>
/// Merchant operations service contract.
/// </summary>
public interface IMerchantService
{
    Task<MerchantProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<MerchantProfileResponse> UpdateMyProfileAsync(Guid userId, UpdateMerchantProfileRequest request, CancellationToken cancellationToken = default);

    Task<ShipmentRequestDetailsResponse> CreateShipmentRequestAsync(Guid userId, CreateShipmentRequestRequest request, CancellationToken cancellationToken = default);

    Task<PaginatedResult<ShipmentRequestSummaryResponse>> GetMyShipmentRequestsAsync(Guid userId, GetMerchantShipmentRequestsQuery query, CancellationToken cancellationToken = default);

    Task<ShipmentRequestDetailsResponse> GetMyShipmentRequestDetailsAsync(Guid userId, Guid shipmentRequestId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShipmentOfferResponse>> GetOffersForMyRequestAsync(Guid userId, Guid shipmentRequestId, CancellationToken cancellationToken = default);

    Task<ShipmentRequestDetailsResponse> AcceptOfferAsync(Guid userId, Guid shipmentRequestId, Guid offerId, CancellationToken cancellationToken = default);

    Task CancelShipmentRequestAsync(Guid userId, Guid shipmentRequestId, CancellationToken cancellationToken = default);

    Task<ShipmentTrackingResponse> GetTrackingAsync(Guid userId, Guid shipmentIdOrRequestId, CancellationToken cancellationToken = default);
}
