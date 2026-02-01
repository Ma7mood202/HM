using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using Microsoft.EntityFrameworkCore;

namespace HM.Infrastructure.Services;

/// <summary>
/// Resolves profile IDs from user ID using DbContext.
/// </summary>
public sealed class CurrentProfileAccessor : ICurrentProfileAccessor
{
    private readonly IApplicationDbContext _db;

    public CurrentProfileAccessor(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<Guid?> GetMerchantProfileIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.MerchantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile?.Id;
    }

    public async Task<Guid?> GetTruckAccountIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var account = await _db.TruckAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        return account?.Id;
    }

    public async Task<Guid?> GetDriverProfileIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return profile?.Id;
    }
}
