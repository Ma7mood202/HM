using AutoMapper;
using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.DTOs.Shipment;
using HM.Application.Common.Models;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HM.Infrastructure.Services;

/// <summary>
/// Merchant operations: shipment requests, offers, accept offer, tracking.
/// </summary>
public sealed class MerchantService : IMerchantService
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;

    public MerchantService(IApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<ShipmentRequestDto> CreateShipmentRequestAsync(Guid merchantProfileId, CreateShipmentRequestDto request, CancellationToken cancellationToken = default)
    {
        var entity = _mapper.Map<ShipmentRequest>(request);
        entity.Id = Guid.NewGuid();
        entity.MerchantProfileId = merchantProfileId;
        entity.Status = ShipmentRequestStatus.Open;
        entity.CreatedAt = DateTime.Now;

        _db.ShipmentRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<ShipmentRequestDto>(entity);
        dto.OffersCount = 0;
        return dto;
    }

    public async Task<PaginatedResult<ShipmentRequestDto>> GetMyShipmentRequestsAsync(Guid merchantProfileId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var query = _db.ShipmentRequests
            .Where(r => r.MerchantProfileId == merchantProfileId)
            .OrderByDescending(r => r.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var offerCounts = await _db.ShipmentOffers
            .Where(o => items.Select(i => i.Id).Contains(o.ShipmentRequestId))
            .GroupBy(o => o.ShipmentRequestId)
            .Select(g => new { RequestId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countLookup = offerCounts.ToDictionary(x => x.RequestId, x => x.Count);

        var dtos = items.Select(r =>
        {
            var dto = _mapper.Map<ShipmentRequestDto>(r);
            dto.OffersCount = countLookup.GetValueOrDefault(r.Id, 0);
            return dto;
        }).ToList();

        return new PaginatedResult<ShipmentRequestDto>
        {
            Items = dtos,
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = total
        };
    }

    public async Task<PaginatedResult<ShipmentOfferDto>> GetShipmentOffersAsync(Guid merchantProfileId, Guid shipmentRequestId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var request = await _db.ShipmentRequests.FindAsync([shipmentRequestId], cancellationToken);
        if (request == null || request.MerchantProfileId != merchantProfileId)
            throw new InvalidOperationException("Shipment request not found.");

        var query = _db.ShipmentOffers
            .Where(o => o.ShipmentRequestId == shipmentRequestId)
            .OrderByDescending(o => o.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var offers = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var truckAccountIds = offers.Select(o => o.TruckAccountId).Distinct().ToList();
        var truckAccounts = await _db.TruckAccounts
            .Where(t => truckAccountIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var dtos = offers.Select(o =>
        {
            var dto = _mapper.Map<ShipmentOfferDto>(o);
            dto.TruckAccountName = truckAccounts.GetValueOrDefault(o.TruckAccountId)?.DisplayName ?? "";
            return dto;
        }).ToList();

        return new PaginatedResult<ShipmentOfferDto>
        {
            Items = dtos,
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = total
        };
    }

    public async Task<ShipmentDetailsDto> AcceptOfferAsync(Guid merchantProfileId, Guid offerId, CancellationToken cancellationToken = default)
    {
        var offer = await _db.ShipmentOffers
            .FirstOrDefaultAsync(o => o.Id == offerId, cancellationToken);
        if (offer == null)
            throw new InvalidOperationException("Offer not found.");

        if (offer.Status != ShipmentOfferStatus.Pending)
            throw new InvalidOperationException("Offer is no longer pending.");

        var request = await _db.ShipmentRequests.FindAsync([offer.ShipmentRequestId], cancellationToken);
        if (request == null || request.MerchantProfileId != merchantProfileId)
            throw new InvalidOperationException("Shipment request not found.");

        if (request.Status != ShipmentRequestStatus.Open)
            throw new InvalidOperationException("Shipment request is no longer open.");

        var truck = await _db.Trucks
            .FirstOrDefaultAsync(t => t.TruckAccountId == offer.TruckAccountId && t.IsActive, cancellationToken);
        if (truck == null)
            throw new InvalidOperationException("Truck account has no active truck.");

        var shipment = new Shipment
        {
            Id = Guid.NewGuid(),
            ShipmentRequestId = request.Id,
            AcceptedOfferId = offer.Id,
            TruckId = truck.Id,
            Status = ShipmentStatus.AwaitingDriver
        };
        _db.Shipments.Add(shipment);

        offer.Status = ShipmentOfferStatus.Accepted;

        var otherOffers = await _db.ShipmentOffers
            .Where(o => o.ShipmentRequestId == request.Id && o.Id != offer.Id && o.Status == ShipmentOfferStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var o in otherOffers)
            o.Status = ShipmentOfferStatus.Rejected;

        request.Status = ShipmentRequestStatus.OfferAccepted;

        await _db.SaveChangesAsync(cancellationToken);

        return await BuildShipmentDetailsDtoAsync(shipment.Id, cancellationToken);
    }

    public async Task<ShipmentTrackingDto> GetShipmentTrackingAsync(Guid merchantProfileId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments
            .FirstOrDefaultAsync(s => s.Id == shipmentId, cancellationToken);
        if (shipment == null)
            throw new InvalidOperationException("Shipment not found.");

        var request = await _db.ShipmentRequests.FindAsync([shipment.ShipmentRequestId], cancellationToken);
        if (request == null || request.MerchantProfileId != merchantProfileId)
            throw new InvalidOperationException("Shipment not found.");

        string? driverName = null;
        if (shipment.DriverProfileId.HasValue)
        {
            var driver = await _db.DriverProfiles.FindAsync([shipment.DriverProfileId.Value], cancellationToken);
            if (driver != null)
                driverName = driver.FullName;
        }

        var truck = await _db.Trucks.FindAsync([shipment.TruckId], cancellationToken);

        return new ShipmentTrackingDto
        {
            ShipmentId = shipment.Id,
            Status = shipment.Status,
            PickupLocation = request.PickupLocation,
            DropoffLocation = request.DropoffLocation,
            DriverName = driverName,
            TruckPlateNumber = truck?.PlateNumber,
            StartedAt = shipment.StartedAt,
            CompletedAt = shipment.CompletedAt
        };
    }

    private async Task<ShipmentDetailsDto> BuildShipmentDetailsDtoAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null)
            throw new InvalidOperationException("Shipment not found.");

        var request = await _db.ShipmentRequests.FindAsync([shipment.ShipmentRequestId], cancellationToken);
        var offer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
        var truck = await _db.Trucks.FindAsync([shipment.TruckId], cancellationToken);
        string? driverName = null;
        if (shipment.DriverProfileId.HasValue)
        {
            var driver = await _db.DriverProfiles.FindAsync([shipment.DriverProfileId.Value], cancellationToken);
            driverName = driver?.FullName;
        }

        return new ShipmentDetailsDto
        {
            Id = shipment.Id,
            ShipmentRequestId = shipment.ShipmentRequestId,
            AcceptedOfferId = shipment.AcceptedOfferId,
            TruckId = shipment.TruckId,
            DriverProfileId = shipment.DriverProfileId,
            PickupLocation = request?.PickupLocation ?? "",
            DropoffLocation = request?.DropoffLocation ?? "",
            CargoDescription = request?.CargoDescription ?? "",
            RequiredTruckType = request?.RequiredTruckType ?? default,
            EstimatedWeight = request?.EstimatedWeight ?? 0,
            Notes = request?.Notes,
            Price = offer?.Price ?? 0,
            TruckPlateNumber = truck?.PlateNumber ?? "",
            DriverName = driverName,
            Status = shipment.Status,
            StartedAt = shipment.StartedAt,
            CompletedAt = shipment.CompletedAt
        };
    }
}
