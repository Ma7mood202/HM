namespace HM.Application.Interfaces.Services;

/// <summary>
/// Resolves current user's profile ID by user type (Merchant / TruckAccount / Driver).
/// Used by WebApi to obtain merchantProfileId, truckAccountId, or driverProfileId from JWT userId.
/// </summary>
public interface ICurrentProfileAccessor
{
    Task<Guid?> GetMerchantProfileIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetTruckAccountIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Guid?> GetDriverProfileIdAsync(Guid userId, CancellationToken cancellationToken = default);
}
