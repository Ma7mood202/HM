using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HM.Application.Interfaces.Persistence;

/// <summary>
/// Abstraction over Entity Framework DbContext.
/// Implementations live in HM.Infrastructure.
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<MerchantProfile> MerchantProfiles { get; }
    DbSet<TruckAccount> TruckAccounts { get; }
    DbSet<Truck> Trucks { get; }
    DbSet<DriverProfile> DriverProfiles { get; }
    DbSet<ShipmentRequest> ShipmentRequests { get; }
    DbSet<ShipmentOffer> ShipmentOffers { get; }
    DbSet<Shipment> Shipments { get; }
    DbSet<DriverInvitation> DriverInvitations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
