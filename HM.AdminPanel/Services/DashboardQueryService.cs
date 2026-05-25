using HM.AdminPanel.ViewModels.Dashboard;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Services;

public class DashboardQueryService : IDashboardQueryService
{
    private readonly ApplicationDbContext _db;
    public DashboardQueryService(ApplicationDbContext db) => _db = db;

    public async Task<DashboardVm> BuildAsync(CancellationToken ct = default)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var since30  = todayUtc.AddDays(-29);

        var vm = new DashboardVm
        {
            TotalMerchants     = await _db.Users.CountAsync(u => u.UserType == UserType.Merchant, ct),
            TotalDrivers       = await _db.Users.CountAsync(u => u.UserType == UserType.Driver, ct),
            TotalTruckAccounts = await _db.Users.CountAsync(u => u.UserType == UserType.TruckAccount, ct),
            TotalTrucks        = await _db.Trucks.CountAsync(ct),
            ActiveShipments    = await _db.Shipments.CountAsync(
                                    s => s.Status != ShipmentStatus.Completed
                                      && s.Status != ShipmentStatus.Cancelled, ct),
            CompletedToday     = await _db.Shipments.CountAsync(
                                    s => s.Status == ShipmentStatus.Completed
                                      && s.CompletedAt >= todayUtc, ct),
        };

        var grouped = await _db.Shipments
            .Where(s => s.StartedAt >= since30)
            .GroupBy(s => s.StartedAt!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        vm.Last30Days = Enumerable.Range(0, 30)
            .Select(i => DateOnly.FromDateTime(since30.AddDays(i)))
            .Select(d => new DayCount(d,
                grouped.FirstOrDefault(x =>
                    DateOnly.FromDateTime(x.Day) == d)?.Count ?? 0))
            .ToList();

        vm.ShipmentsByStatus = await _db.Shipments
            .GroupBy(s => s.Status)
            .Select(g => new StatusCount(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        vm.RecentActivity = await _db.Shipments
            .OrderByDescending(s => s.StartedAt)
            .Take(10)
            .Select(s => new RecentItem(
                "Shipment",
                "Shipment " + s.Id.ToString(),
                s.Status.ToString(),
                s.StartedAt ?? DateTime.UtcNow))
            .ToListAsync(ct);

        return vm;
    }
}
