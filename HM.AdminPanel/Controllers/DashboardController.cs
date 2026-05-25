using HM.AdminPanel.Authorization;
using HM.AdminPanel.Services;
using HM.AdminPanel.ViewModels.Dashboard;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class DashboardController : Controller
{
    private readonly IDashboardQueryService _query;
    public DashboardController(IDashboardQueryService query) => _query = query;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";
        var vm = await _query.BuildAsync();
        return View(vm);
    }

    public async Task<IActionResult> LiveMap(
        [FromServices] ApplicationDbContext db,
        [FromServices] IConfiguration cfg)
    {
        ViewData["Title"] = "Live Map";

        var pins = await db.Shipments
            .Where(s => s.Status != ShipmentStatus.Completed && s.Status != ShipmentStatus.Cancelled)
            .Select(s => new ActiveShipmentPin(s.Id, s.CurrentLat, s.CurrentLng, s.Status.ToString()))
            .ToListAsync();

        return View(new LiveMapVm
        {
            HubUrl = cfg["AdminPanel:SignalRHubUrl"] ?? "",
            Pins   = pins
        });
    }
}
