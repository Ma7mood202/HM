using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.Common;
using HM.AdminPanel.ViewModels.Shipments;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class ShipmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ShipmentsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ShipmentFilterVm filter)
    {
        ViewData["Title"] = "Shipments";

        var q = _db.Shipments.AsQueryable();
        if (filter.Status.HasValue) q = q.Where(s => s.Status == filter.Status);
        if (filter.From.HasValue)   q = q.Where(s => s.StartedAt >= filter.From);
        if (filter.To.HasValue)     q = q.Where(s => s.StartedAt <= filter.To);
        if (filter.DriverId.HasValue) q = q.Where(s => s.DriverProfileId == filter.DriverId);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(s => s.StartedAt ?? DateTime.MinValue)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new ShipmentListItemVm
            {
                Id = s.Id, Status = s.Status, StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt, TruckId = s.TruckId,
                DriverId = s.DriverProfileId
            })
            .ToListAsync();

        return View(new ShipmentListVm
        {
            Filter = filter,
            Result = new PagedResult<ShipmentListItemVm>
            {
                Items = items, Page = filter.Page, PageSize = filter.PageSize, Total = total
            }
        });
    }

    [HttpGet("Shipments/Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Shipment";
        var s = await _db.Shipments.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();

        return View(new ShipmentDetailVm
        {
            Id = s.Id, Status = s.Status, StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt, AssignedAt = s.AssignedAt,
            CurrentLat = s.CurrentLat, CurrentLng = s.CurrentLng,
            LocationUpdatedAt = s.LocationUpdatedAt,
            ShipmentRequestId = s.ShipmentRequestId, AcceptedOfferId = s.AcceptedOfferId,
            TruckId = s.TruckId, DriverProfileId = s.DriverProfileId
        });
    }

    [HttpPost("Shipments/Cancel/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Cancel(Guid id, string? reason)
    {
        var s = await _db.Shipments.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        s.Status = ShipmentStatus.Cancelled;
        s.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Shipment cancelled. Reason: {reason ?? "(none)"}";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Shipments/Reassign/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Reassign(Guid id, Guid newDriverId)
    {
        var s = await _db.Shipments.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();
        s.DriverProfileId = newDriverId;
        s.AssignedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Driver reassigned.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
