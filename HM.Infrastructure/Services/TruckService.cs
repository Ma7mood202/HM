using AutoMapper;
using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.DTOs.Shipment;
using HM.Application.Common.DTOs.Truck;
using HM.Application.Common.Models;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HM.Infrastructure.Services;

/// <summary>
/// Truck account operations: browse requests, submit offer, assign self, generate driver invitation, list shipments.
/// </summary>
public sealed class TruckService : ITruckService
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;

    public TruckService(IApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<PaginatedResult<ShipmentRequestDto>> GetOpenShipmentRequestsAsync(ShipmentRequestFilterDto? filter, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        IQueryable<HM.Domain.Entities.ShipmentRequest> query = _db.ShipmentRequests
            .Where(r => r.Status == ShipmentRequestStatus.Open);

        if (filter != null)
        {
            if (filter.RequiredTruckType.HasValue)
                query = query.Where(r => r.RequiredTruckType == filter.RequiredTruckType.Value);
            if (filter.MinWeight.HasValue)
                query = query.Where(r => r.EstimatedWeight >= filter.MinWeight.Value);
            if (filter.MaxWeight.HasValue)
                query = query.Where(r => r.EstimatedWeight <= filter.MaxWeight.Value);
            if (!string.IsNullOrWhiteSpace(filter.PickupLocationSearch))
                query = query.Where(r => r.PickupLocation.Contains(filter.PickupLocationSearch));
            if (!string.IsNullOrWhiteSpace(filter.DropoffLocationSearch))
                query = query.Where(r => r.DropoffLocation.Contains(filter.DropoffLocationSearch));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var requestIds = items.Select(r => r.Id).ToList();
        var offerCounts = await _db.ShipmentOffers
            .Where(o => requestIds.Contains(o.ShipmentRequestId))
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

    public async Task<ShipmentOfferDto> SubmitOfferAsync(Guid truckAccountId, SubmitOfferRequest request, CancellationToken cancellationToken = default)
    {
        var shipmentRequest = await _db.ShipmentRequests.FindAsync([request.ShipmentRequestId], cancellationToken);
        if (shipmentRequest == null)
            throw new InvalidOperationException("Shipment request not found.");
        if (shipmentRequest.Status != ShipmentRequestStatus.Open)
            throw new InvalidOperationException("Shipment request is no longer open.");

        var expirationAt = request.ExpirationAt ?? DateTime.Now.AddDays(7);

        var offer = new ShipmentOffer
        {
            Id = Guid.NewGuid(),
            ShipmentRequestId = request.ShipmentRequestId,
            TruckAccountId = truckAccountId,
            Price = request.Price,
            Notes = request.Notes,
            Status = ShipmentOfferStatus.Pending,
            CreatedAt = DateTime.Now,
            ExpirationAt = expirationAt
        };
        _db.ShipmentOffers.Add(offer);
        await _db.SaveChangesAsync(cancellationToken);

        var truckAccount = await _db.TruckAccounts.FindAsync([truckAccountId], cancellationToken);
        var dto = _mapper.Map<ShipmentOfferDto>(offer);
        dto.TruckAccountName = truckAccount?.DisplayName ?? "";
        return dto;
    }

    public async Task<ShipmentDetailsDto> AssignSelfAsDriverAsync(Guid truckAccountId, Guid shipmentId, Guid truckId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null)
            throw new InvalidOperationException("Shipment not found.");
        if (shipment.TruckId != truckId)
            throw new InvalidOperationException("Truck does not belong to this shipment.");

        var truck = await _db.Trucks.FindAsync([truckId], cancellationToken);
        if (truck == null || truck.TruckAccountId != truckAccountId)
            throw new InvalidOperationException("Truck not found or not owned by this account.");

        var truckAccount = await _db.TruckAccounts.FindAsync([truckAccountId], cancellationToken);
        if (truckAccount == null)
            throw new InvalidOperationException("Truck account not found.");

        var user = await _db.Users.FindAsync([truckAccount.UserId], cancellationToken);
        if (user == null)
            throw new InvalidOperationException("Truck account has no linked user.");

        var driverProfile = await _db.DriverProfiles
            .FirstOrDefaultAsync(d => d.UserId == truckAccount.UserId, cancellationToken);
        if (driverProfile == null)
        {
            driverProfile = new DriverProfile
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                FullName = user.FullName,
                IsVerified = false,
                CreatedAt = DateTime.Now
            };
            _db.DriverProfiles.Add(driverProfile);
            await _db.SaveChangesAsync(cancellationToken);
        }

        shipment.DriverProfileId = driverProfile.Id;
        shipment.Status = ShipmentStatus.Ready;
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildShipmentDetailsDtoAsync(shipment.Id, cancellationToken);
    }

    public async Task<DriverInvitationDto> GenerateDriverInvitationAsync(Guid truckAccountId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null)
            throw new InvalidOperationException("Shipment not found.");

        var offer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
        if (offer == null || offer.TruckAccountId != truckAccountId)
            throw new InvalidOperationException("Shipment not found or not owned by this account.");

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace("/", "_").Replace("+", "-");
        var invitation = new DriverInvitation
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            Token = token,
            ExpiresAt = DateTime.Now.AddDays(7),
            IsUsed = false,
            CreatedAt = DateTime.Now
        };
        _db.DriverInvitations.Add(invitation);
        await _db.SaveChangesAsync(cancellationToken);

        return new DriverInvitationDto
        {
            Id = invitation.Id,
            ShipmentId = invitation.ShipmentId,
            Token = invitation.Token,
            InvitationUrl = $"invite/{invitation.Token}",
            ExpiresAt = invitation.ExpiresAt,
            CreatedAt = invitation.CreatedAt
        };
    }

    public async Task<PaginatedResult<ShipmentListItemDto>> GetMyShipmentsAsync(Guid truckAccountId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var offerIds = await _db.ShipmentOffers
            .Where(o => o.TruckAccountId == truckAccountId)
            .Select(o => o.Id)
            .ToListAsync(cancellationToken);

        var query = _db.Shipments
            .Where(s => offerIds.Contains(s.AcceptedOfferId))
            .OrderByDescending(s => s.Id);

        var total = await query.CountAsync(cancellationToken);

        var shipments = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var requestIds = shipments.Select(s => s.ShipmentRequestId).Distinct().ToList();
        var requests = await _db.ShipmentRequests
            .Where(r => requestIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var offerIdToOffer = await _db.ShipmentOffers
            .Where(o => shipments.Select(s => s.AcceptedOfferId).Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, cancellationToken);

        var truckIds = shipments.Select(s => s.TruckId).Distinct().ToList();
        var trucks = await _db.Trucks
            .Where(t => truckIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var driverIds = shipments.Where(s => s.DriverProfileId.HasValue).Select(s => s.DriverProfileId!.Value).Distinct().ToList();
        var drivers = driverIds.Any()
            ? await _db.DriverProfiles.Where(d => driverIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken)
            : new Dictionary<Guid, DriverProfile>();

        var items = shipments.Select(s =>
        {
            var req = requests.GetValueOrDefault(s.ShipmentRequestId);
            var off = offerIdToOffer.GetValueOrDefault(s.AcceptedOfferId);
            var truck = trucks.GetValueOrDefault(s.TruckId);
            var driver = s.DriverProfileId.HasValue ? drivers.GetValueOrDefault(s.DriverProfileId.Value) : null;
            return new ShipmentListItemDto
            {
                ShipmentId = s.Id,
                PickupLocation = req?.PickupLocation ?? "",
                DropoffLocation = req?.DropoffLocation ?? "",
                CargoDescription = req?.CargoDescription ?? "",
                Status = s.Status,
                Price = off?.Price ?? 0,
                DriverName = driver?.FullName,
                TruckPlateNumber = truck?.PlateNumber,
                CreatedAt = req?.CreatedAt ?? DateTime.Now
            };
        }).ToList();

        return new PaginatedResult<ShipmentListItemDto>
        {
            Items = items,
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = total
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
