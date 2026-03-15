using HM.Application.Interfaces.Persistence;
using HM.Application.Common.DTOs.Location;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Hm.WebApi.Controllers;

/// <summary>
/// Governorates and regions for pickup/drop-off selection (used by merchant and truck flows).
/// </summary>
[ApiController]
[Route("api")]
public class LocationController : ControllerBase
{
    private readonly IApplicationDbContext _db;

    public LocationController(IApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>Get all governorates (for pickup and drop-off).</summary>
    [HttpGet("governorates")]
    public async Task<ActionResult<IReadOnlyList<GovernorateDto>>> GetGovernorates(CancellationToken cancellationToken)
    {
        var list = await _db.Governorates
            .AsNoTracking()
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.NameEn)
            .Select(g => new GovernorateDto
            {
                Id = g.Id,
                NameAr = g.NameAr,
                NameEn = g.NameEn,
                SortOrder = g.SortOrder
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }

    /// <summary>Get regions, optionally filtered by governorate (for pickup and drop-off).</summary>
    [HttpGet("regions")]
    public async Task<ActionResult<IReadOnlyList<RegionDto>>> GetRegions(
        [FromQuery] Guid? governorateId,
        CancellationToken cancellationToken)
    {
        var query = _db.Regions.AsNoTracking();
        if (governorateId.HasValue)
            query = query.Where(r => r.GovernorateId == governorateId.Value);

        var list = await query
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.NameEn)
            .Select(r => new RegionDto
            {
                Id = r.Id,
                GovernorateId = r.GovernorateId,
                NameAr = r.NameAr,
                NameEn = r.NameEn,
                SortOrder = r.SortOrder
            })
            .ToListAsync(cancellationToken);
        return Ok(list);
    }
}
