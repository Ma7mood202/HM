using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.Common;
using HM.AdminPanel.ViewModels.TruckAccounts;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class TruckAccountsController : Controller
{
    private readonly ApplicationDbContext _db;
    public TruckAccountsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TruckAccountFilterVm filter)
    {
        ViewData["Title"] = "Truck Accounts";

        var q = _db.Users.Where(u => u.UserType == UserType.TruckAccount);
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
            .Select(u => new TruckAccountListItemVm
            {
                Id = u.Id, FullName = u.FullName, PhoneNumber = u.PhoneNumber,
                Email = u.Email, IsActive = u.IsActive, IsBlocked = u.IsBlocked,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return View(new TruckAccountListVm
        {
            Filter = filter,
            Result = new PagedResult<TruckAccountListItemVm>
            {
                Items = items, Page = filter.Page, PageSize = filter.PageSize, Total = total
            }
        });
    }

    [HttpGet("TruckAccounts/Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Truck Account";
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.TruckAccount);
        if (u is null) return NotFound();

        // User -> TruckAccount (by UserId) -> Truck (by TruckAccountId)
        var accountId = await _db.TruckAccounts
            .Where(t => t.UserId == id)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync();
        var owned = accountId.HasValue
            ? await _db.Trucks.CountAsync(t => t.TruckAccountId == accountId.Value)
            : 0;

        return View(new TruckAccountDetailVm
        {
            Id = u.Id, FullName = u.FullName, PhoneNumber = u.PhoneNumber, Email = u.Email,
            IsActive = u.IsActive, IsBlocked = u.IsBlocked, BlockedAt = u.BlockedAt,
            BlockedReason = u.BlockedReason, CreatedAt = u.CreatedAt,
            OwnedTrucks = owned
        });
    }

    [HttpPost("TruckAccounts/Block/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Block(Guid id, string? reason)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.TruckAccount);
        if (u is null) return NotFound();
        u.IsBlocked = true;
        u.BlockedAt = DateTime.UtcNow;
        u.BlockedReason = reason;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck account blocked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("TruckAccounts/Unblock/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Unblock(Guid id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.TruckAccount);
        if (u is null) return NotFound();
        u.IsBlocked = false;
        u.BlockedAt = null;
        u.BlockedReason = null;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck account unblocked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("TruckAccounts/Verify/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Verify(Guid id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.TruckAccount);
        if (u is null) return NotFound();
        u.IsActive = true;
        u.IsOtpVerified = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck account verified.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
