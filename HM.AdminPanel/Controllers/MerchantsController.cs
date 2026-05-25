using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.Common;
using HM.AdminPanel.ViewModels.Merchants;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class MerchantsController : Controller
{
    private readonly ApplicationDbContext _db;
    public MerchantsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MerchantFilterVm filter)
    {
        ViewData["Title"] = "Merchants";

        var q = _db.Users.Where(u => u.UserType == UserType.Merchant);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(u => u.FullName.Contains(s)
                          || u.PhoneNumber.Contains(s)
                          || u.Email.Contains(s));
        }
        if (filter.IsBlocked.HasValue) q = q.Where(u => u.IsBlocked == filter.IsBlocked);
        if (filter.IsActive.HasValue)  q = q.Where(u => u.IsActive  == filter.IsActive);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(u => u.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(u => new MerchantListItemVm
            {
                Id = u.Id, FullName = u.FullName, PhoneNumber = u.PhoneNumber,
                Email = u.Email, IsActive = u.IsActive, IsBlocked = u.IsBlocked,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return View(new MerchantListVm
        {
            Filter = filter,
            Result = new PagedResult<MerchantListItemVm>
            {
                Items = items, Page = filter.Page, PageSize = filter.PageSize, Total = total
            }
        });
    }

    [HttpGet("Merchants/Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Merchant";
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Merchant);
        if (u is null) return NotFound();

        // User -> MerchantProfile (by UserId) -> ShipmentRequest (by MerchantProfileId)
        var profileId = await _db.MerchantProfiles
            .Where(m => m.UserId == id)
            .Select(m => (Guid?)m.Id)
            .FirstOrDefaultAsync();
        var count = profileId.HasValue
            ? await _db.ShipmentRequests.CountAsync(r => r.MerchantProfileId == profileId.Value)
            : 0;

        return View(new MerchantDetailVm
        {
            Id = u.Id, FullName = u.FullName, PhoneNumber = u.PhoneNumber, Email = u.Email,
            IsActive = u.IsActive, IsBlocked = u.IsBlocked, BlockedAt = u.BlockedAt,
            BlockedReason = u.BlockedReason, CreatedAt = u.CreatedAt, ShipmentCount = count
        });
    }

    [HttpPost("Merchants/Block/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Block(Guid id, string? reason)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Merchant);
        if (u is null) return NotFound();
        u.IsBlocked = true;
        u.BlockedAt = DateTime.UtcNow;
        u.BlockedReason = reason;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Merchant blocked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Merchants/Unblock/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Unblock(Guid id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Merchant);
        if (u is null) return NotFound();
        u.IsBlocked = false;
        u.BlockedAt = null;
        u.BlockedReason = null;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Merchant unblocked.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Merchants/Verify/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Verify(Guid id)
    {
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Merchant);
        if (u is null) return NotFound();
        u.IsActive = true;
        u.IsOtpVerified = true;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Merchant verified.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
