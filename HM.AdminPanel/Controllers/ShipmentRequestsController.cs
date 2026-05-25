using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.ShipmentRequests;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class ShipmentRequestsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ShipmentRequestsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Shipment Requests";

        var items = await _db.ShipmentRequests
            .OrderByDescending(r => r.CreatedAt)
            .Take(200)
            .Select(r => new ShipmentRequestListItemVm
            {
                Id = r.Id,
                RequestNumber = r.RequestNumber,
                Status = r.Status,
                MerchantProfileId = r.MerchantProfileId,
                CreatedAt = r.CreatedAt,
                OffersCount = _db.ShipmentOffers.Count(o => o.ShipmentRequestId == r.Id)
            })
            .ToListAsync();
        return View(items);
    }
}
