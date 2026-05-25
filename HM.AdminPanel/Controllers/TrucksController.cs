using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.Common;
using HM.AdminPanel.ViewModels.Trucks;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class TrucksController : Controller
{
    private readonly ApplicationDbContext _db;
    public TrucksController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TruckFilterVm filter)
    {
        ViewData["Title"] = "Trucks";

        // TruckAccount.Id -> User.Id via TruckAccount.UserId join with Users for owner name
        var q = from t in _db.Trucks
                join ta in _db.TruckAccounts on t.TruckAccountId equals ta.Id into taj
                from ta in taj.DefaultIfEmpty()
                join u in _db.Users on (ta != null ? ta.UserId : (Guid?)null) equals u.Id into uj
                from u in uj.DefaultIfEmpty()
                select new { t, u };

        if (filter.Status.HasValue)
            q = q.Where(x => x.t.ApprovalStatus == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(x => x.t.PlateNumber.Contains(s)
                          || (x.u != null && (x.u.PhoneNumber.Contains(s) || x.u.FullName.Contains(s))));
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(x => x.t.ApprovalStatus).ThenBy(x => x.t.PlateNumber)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new TruckListItemVm
            {
                Id = x.t.Id, PlateNumber = x.t.PlateNumber, TruckType = x.t.TruckType,
                BodyType = x.t.BodyType, MaxWeight = x.t.MaxWeight,
                ApprovalStatus = x.t.ApprovalStatus, IsActive = x.t.IsActive,
                OwnerName = x.u != null ? x.u.FullName : ""
            })
            .ToListAsync();

        return View(new TruckListVm
        {
            Filter = filter,
            Result = new PagedResult<TruckListItemVm>
            {
                Items = items, Page = filter.Page, PageSize = filter.PageSize, Total = total
            }
        });
    }

    [HttpGet("Trucks/Details/{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Truck";
        var x = await (from t in _db.Trucks
                       join ta in _db.TruckAccounts on t.TruckAccountId equals ta.Id into taj
                       from ta in taj.DefaultIfEmpty()
                       join u in _db.Users on (ta != null ? ta.UserId : (Guid?)null) equals u.Id into uj
                       from u in uj.DefaultIfEmpty()
                       where t.Id == id
                       select new { t, u }).FirstOrDefaultAsync();
        if (x is null) return NotFound();

        var shipments = await _db.Shipments.CountAsync(s => s.TruckId == id);

        return View(new TruckDetailVm
        {
            Id = x.t.Id, PlateNumber = x.t.PlateNumber, TruckType = x.t.TruckType,
            BodyType = x.t.BodyType, MaxWeight = x.t.MaxWeight,
            IsActive = x.t.IsActive, ApprovalStatus = x.t.ApprovalStatus,
            RejectionReason = x.t.RejectionReason, TruckAccountId = x.t.TruckAccountId,
            OwnerName = x.u != null ? x.u.FullName : "",
            ShipmentsCount = shipments
        });
    }

    [HttpPost("Trucks/Approve/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Approve(Guid id)
    {
        var t = await _db.Trucks.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.ApprovalStatus = TruckApprovalStatus.Approved;
        t.RejectionReason = null;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck approved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Trucks/Reject/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Reject(Guid id, string? reason)
    {
        var t = await _db.Trucks.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.ApprovalStatus = TruckApprovalStatus.Rejected;
        t.RejectionReason = reason;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Trucks/Suspend/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var t = await _db.Trucks.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.IsActive = false;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck suspended.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
