using HM.Application.Interfaces.Persistence;
using HM.Domain.Entities;
using HM.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HM.Infrastructure.Data;

/// <summary>
/// Entity Framework DbContext with ASP.NET Core Identity.
/// Domain entities and Identity share the same database; Domain User and ApplicationUser share the same Id (Guid).
/// </summary>
public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>Domain users (business); Identity users are in base.Users (AspNetUsers).</summary>
    public new DbSet<User> Users => Set<User>();
    public DbSet<MerchantProfile> MerchantProfiles => Set<MerchantProfile>();
    public DbSet<TruckAccount> TruckAccounts => Set<TruckAccount>();
    public DbSet<Truck> Trucks => Set<Truck>();
    public DbSet<DriverProfile> DriverProfiles => Set<DriverProfile>();
    public DbSet<ShipmentRequest> ShipmentRequests => Set<ShipmentRequest>();
    public DbSet<ShipmentOffer> ShipmentOffers => Set<ShipmentOffer>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<DriverInvitation> DriverInvitations => Set<DriverInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
