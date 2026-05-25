using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.Common;
using HM.AdminPanel.ViewModels.Drivers;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class DriversController : Controller
{
    private readonly ApplicationDbContext _db;
    public DriversController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] DriverFilterVm filter)
    {
        ViewData["Title"] = "Drivers";

        var q = _db.Users.Where(u => u.UserType == UserType.Driver);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(u => u.FullName.Contains(s) || u.PhoneNumber.Contains(s) || u.Email.Contains(s));
        }
        if (filter.IsBlocked.HasValue) q = q.Where(u => u.IsBlocked == filter.IsBlocked);
        if (filter.IsActive.HasValue)  q = q.Where(u => u.IsActive  == filter.IsActive);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(u => u.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(u => new DriverListItemVm
            {
                Id = u.Id, FullName = u.FullName, PhoneNumber = u.PhoneNumber,
                Email = u.Email, IsActive = u.IsActive, IsBlocked = u.IsBlocked,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return View(new DriverListVm
        {
            Filter = filter,
            Result = new PagedResult<DriverListItemVm>
            {
                Items = items, Page = filter.Page, PageSize = filter.PageSize, Total = total
            }
        });
    }

    [HttpGet("Drivers/Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Driver";
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Driver);
        if (u is null) return NotFound();

        // User -> DriverProfile (by UserId) -> Shipment (by DriverProfileId)
        var profileId = await _db.DriverProfiles
            .Where(d => d.UserId == id)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync();
        var count = profileId.HasValue
            ? await _db.Shipments.CountAsync(s => s.DriverProfileId == profileId.Value
                                               && s.Status == ShipmentStatus.Completed)
            : 0;

        return View(new DriverDetailVm
        {
            Id = u.Id, FullName = u.FullName, PhoneNumber = u.PhoneNumber, Email = u.Email,
            IsActive = u.IsActive, IsBlocked = u.IsBlocked, BlockedAt = u.BlockedAt,
            BlockedReason = u.BlockedReason, CreatedAt = u.CreatedAt,
            CompletedShipmentCount = count
        });
    }

    [HttpPost("Drivers/Block/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Block(Guid id, string? reason)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Driver);
        if (u is null) return NotFound();
        u.IsBlocked = true;
        u.BlockedAt = DateTime.UtcNow;
        u.BlockedReason = reason;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Driver blocked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Drivers/Unblock/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Unblock(Guid id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Driver);
        if (u is null) return NotFound();
        u.IsBlocked = false;
        u.BlockedAt = null;
        u.BlockedReason = null;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Driver unblocked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Drivers/Verify/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Verify(Guid id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Driver);
        if (u is null) return NotFound();
        u.IsActive = true;
        u.IsOtpVerified = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Driver verified.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
