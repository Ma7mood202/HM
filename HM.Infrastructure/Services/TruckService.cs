using AutoMapper;
using HM.Application.Common.DTOs.Driver;
using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.DTOs.Shipment;
using HM.Application.Common.DTOs.Truck;
using HM.Application.Common.Models;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HM.Infrastructure.Services;

/// <summary>
/// Truck account operations: browse requests, submit offer, assign self, generate driver invitation, list shipments.
/// </summary>
public sealed class TruckService : ITruckService
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly ILogger<TruckService> _logger;

    public TruckService(IApplicationDbContext db, IMapper mapper, INotificationService notificationService, ILogger<TruckService> logger)
    {
        _db = db;
        _mapper = mapper;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<TruckProfileDto> GetMyProfileAsync(Guid truckUserId, CancellationToken cancellationToken = default)
    {
        var account = await _db.TruckAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.UserId == truckUserId, cancellationToken);
        if (account == null)
            throw new KeyNotFoundException("Truck account profile not found.");

        var user = await _db.Users.FindAsync([truckUserId], cancellationToken);
        var dto = _mapper.Map<TruckProfileDto>(account);
        dto.PhoneNumber = user?.PhoneNumber;
        dto.FullName = !string.IsNullOrWhiteSpace(account.FullName) ? account.FullName : (user?.FullName ?? string.Empty);
        dto.HasNationalId = !string.IsNullOrEmpty(account.NationalIdFrontImageUrl) && !string.IsNullOrEmpty(account.NationalIdBackImageUrl);
        return dto;
    }

    public async Task<TruckProfileDto> UpdateMyProfileAsync(Guid truckUserId, UpdateTruckProfileRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _db.TruckAccounts.FirstOrDefaultAsync(t => t.UserId == truckUserId, cancellationToken);
        if (account == null)
            throw new KeyNotFoundException("Truck account profile not found.");

        if (!string.IsNullOrWhiteSpace(request.FullName))
            account.FullName = request.FullName.Trim();
        if (request.AvatarUrl != null)
            account.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        if (request.NationalIdFrontImageUrl != null)
            account.NationalIdFrontImageUrl = string.IsNullOrWhiteSpace(request.NationalIdFrontImageUrl) ? null : request.NationalIdFrontImageUrl.Trim();
        if (request.NationalIdBackImageUrl != null)
            account.NationalIdBackImageUrl = string.IsNullOrWhiteSpace(request.NationalIdBackImageUrl) ? null : request.NationalIdBackImageUrl.Trim();

        var user = await _db.Users.FindAsync([truckUserId], cancellationToken);
        if (user != null && !string.IsNullOrWhiteSpace(request.PhoneNumber))
            user.PhoneNumber = request.PhoneNumber.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<TruckProfileDto>(account);
        dto.PhoneNumber = user?.PhoneNumber;
        dto.FullName = !string.IsNullOrWhiteSpace(account.FullName) ? account.FullName : (user?.FullName ?? string.Empty);
        dto.HasNationalId = !string.IsNullOrEmpty(account.NationalIdFrontImageUrl) && !string.IsNullOrEmpty(account.NationalIdBackImageUrl);
        return dto;
    }

    public async Task<TruckProfileDto> UploadNationalIdAsync(Guid truckAccountId, UploadTruckNationalIdRequest request, CancellationToken cancellationToken = default)
    {
        var account = await _db.TruckAccounts.FindAsync([truckAccountId], cancellationToken);
        if (account == null)
            throw new InvalidOperationException("Truck account not found.");

        if (!string.IsNullOrWhiteSpace(request.NationalIdFrontImageUrl))
            account.NationalIdFrontImageUrl = request.NationalIdFrontImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.NationalIdBackImageUrl))
            account.NationalIdBackImageUrl = request.NationalIdBackImageUrl.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        var user = await _db.Users.FindAsync([account.UserId], cancellationToken);
        var dto = _mapper.Map<TruckProfileDto>(account);
        dto.PhoneNumber = user?.PhoneNumber;
        dto.FullName = !string.IsNullOrWhiteSpace(account.FullName) ? account.FullName : (user?.FullName ?? string.Empty);
        dto.HasNationalId = !string.IsNullOrEmpty(account.NationalIdFrontImageUrl) && !string.IsNullOrEmpty(account.NationalIdBackImageUrl);
        return dto;
    }

    public async Task<PaginatedResult<ShipmentListItemDto>> GetOpenShipmentRequestsAsync(ShipmentRequestFilterDto? filter, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        IQueryable<HM.Domain.Entities.ShipmentRequest> query = _db.ShipmentRequests
            .Where(r => r.Status == ShipmentRequestStatus.Open);

        if (filter != null)
        {
            var truckType = filter.RequiredTruckType ?? filter.TruckType;
            if (truckType.HasValue)
                query = query.Where(r => r.RequiredTruckType == truckType.Value);
            if (filter.TruckBodyType.HasValue)
                query = query.Where(r => r.RequiredTruckBodyType == filter.TruckBodyType.Value);
            if (filter.MinWeight.HasValue)
                query = query.Where(r => r.EstimatedWeightTon >= filter.MinWeight.Value);
            if (filter.MaxWeight.HasValue)
                query = query.Where(r => r.EstimatedWeightTon <= filter.MaxWeight.Value);
            if (filter.ParcelWeightTon.HasValue)
                query = query.Where(r => r.EstimatedWeightTon <= filter.ParcelWeightTon.Value);
            if (filter.PickupRegionId.HasValue)
                query = query.Where(r => r.PickupRegionId == filter.PickupRegionId.Value);
            if (filter.DropoffRegionId.HasValue)
                query = query.Where(r => r.DropoffRegionId == filter.DropoffRegionId.Value);
            var fromRegion = filter.FromRegion ?? filter.PickupLocationSearch;
            var toRegion = filter.ToRegion ?? filter.DropoffLocationSearch;
            if (!string.IsNullOrWhiteSpace(fromRegion))
                query = query.Where(r => r.PickupLocation.Contains(fromRegion));
            if (!string.IsNullOrWhiteSpace(toRegion))
                query = query.Where(r => r.DropoffLocation.Contains(toRegion));
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

        var regionIds = items.SelectMany(r => new[] { r.PickupRegionId, r.DropoffRegionId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var regions = regionIds.Any() ? await _db.Regions.Where(reg => regionIds.Contains(reg.Id)).ToDictionaryAsync(reg => reg.Id, cancellationToken) : new Dictionary<Guid, Region>();

        var dtos = items.Select(r => new ShipmentListItemDto
        {
            ShipmentRequestId = r.Id,
            ShipmentId = null,
            ReferenceNumber = r.RequestNumber,
            PickupLocation = r.PickupLocation,
            DropoffLocation = r.DropoffLocation,
            PickupRegion = r.PickupRegionId.HasValue && regions.TryGetValue(r.PickupRegionId.Value, out var pr) ? pr.NameEn : null,
            DropoffRegion = r.DropoffRegionId.HasValue && regions.TryGetValue(r.DropoffRegionId.Value, out var dr) ? dr.NameEn : null,
            CargoDescription = r.CargoDescription,
            RequiredTruckType = r.RequiredTruckType,
            EstimatedWeight = r.EstimatedWeightTon,
            EstimatedWeightTon = r.EstimatedWeightTon,
            ParcelType = r.ParcelType,
            OffersCount = countLookup.GetValueOrDefault(r.Id, 0),
            Status = null,
            Price = 0,
            DriverName = null,
            TruckPlateNumber = null,
            CreatedAt = r.CreatedAt
        }).ToList();

        return new PaginatedResult<ShipmentListItemDto>
        {
            Items = dtos,
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = total
        };
    }

    public async Task<ShipmentDetailsDto> GetShipmentRequestDetailsAsync(Guid shipmentRequestId, CancellationToken cancellationToken = default)
    {
        var request = await _db.ShipmentRequests.FindAsync([shipmentRequestId], cancellationToken);
        if (request == null)
            throw new KeyNotFoundException("Shipment request not found.");

        string? pickupRegionName = null, dropoffRegionName = null;
        if (request.PickupRegionId.HasValue)
            pickupRegionName = await _db.Regions.Where(r => r.Id == request.PickupRegionId.Value).Select(r => r.NameEn).FirstOrDefaultAsync(cancellationToken);
        if (request.DropoffRegionId.HasValue)
            dropoffRegionName = await _db.Regions.Where(r => r.Id == request.DropoffRegionId.Value).Select(r => r.NameEn).FirstOrDefaultAsync(cancellationToken);

        var shipment = await _db.Shipments.FirstOrDefaultAsync(s => s.ShipmentRequestId == shipmentRequestId, cancellationToken);
        string? driverName = null, driverImage = null, truckPlateNumber = null;
        TruckType? truckType = null;
        if (shipment != null)
        {
            var truck = await _db.Trucks.FindAsync([shipment.TruckId], cancellationToken);
            truckPlateNumber = truck?.PlateNumber;
            truckType = truck?.TruckType;
            if (shipment.DriverProfileId.HasValue)
            {
                var driver = await _db.DriverProfiles.FindAsync([shipment.DriverProfileId.Value], cancellationToken);
                driverName = driver?.FullName;
                driverImage = driver?.AvatarUrl;
            }
        }

        return new ShipmentDetailsDto
        {
            Id = default,
            ShipmentRequestId = request.Id,
            AcceptedOfferId = default,
            TruckId = default,
            DriverProfileId = null,
            ReferenceNumber = request.RequestNumber,
            PickupLocation = request.PickupLocation,
            DropoffLocation = request.DropoffLocation,
            PickupRegion = pickupRegionName,
            DropoffRegion = dropoffRegionName,
            CargoDescription = request.CargoDescription,
            RequiredTruckType = request.RequiredTruckType,
            EstimatedWeight = request.EstimatedWeightTon,
            Notes = request.Notes,
            Price = 0,
            TruckPlateNumber = truckPlateNumber ?? "",
            DriverName = driverName,
            DriverImage = driverImage,
            TruckType = truckType,
            Status = default,
            StartedAt = null,
            CompletedAt = null
        };
    }

    public async Task<ShipmentOfferDto> SubmitOfferAsync(Guid truckAccountId, SubmitOfferRequest request, CancellationToken cancellationToken = default)
    {
        var hasActiveTruck = await _db.Trucks
            .AnyAsync(t => t.TruckAccountId == truckAccountId && t.IsActive, cancellationToken);
        if (!hasActiveTruck)
            throw new InvalidOperationException("You must add at least one active truck before submitting offers.");

        var shipmentRequest = await _db.ShipmentRequests.FindAsync([request.ShipmentRequestId], cancellationToken);
        if (shipmentRequest == null)
            throw new KeyNotFoundException("Shipment request not found.");
        if (shipmentRequest.Status != ShipmentRequestStatus.Open)
            throw new InvalidOperationException("Shipment request is no longer open.");

        var alreadyOffered = await _db.ShipmentOffers
            .AnyAsync(o => o.ShipmentRequestId == request.ShipmentRequestId && o.TruckAccountId == truckAccountId, cancellationToken);
        if (alreadyOffered)
            throw new InvalidOperationException("You have already submitted an offer for this shipment request.");

        var offer = new ShipmentOffer
        {
            Id = Guid.NewGuid(),
            ShipmentRequestId = request.ShipmentRequestId,
            TruckAccountId = truckAccountId,
            Price = request.Price,
            Notes = request.Notes,
            Status = ShipmentOfferStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            ExpirationAt = null
        };
        _db.ShipmentOffers.Add(offer);
        await _db.SaveChangesAsync(cancellationToken);

        var truckAccount = await _db.TruckAccounts.FindAsync([truckAccountId], cancellationToken);
        var dto = _mapper.Map<ShipmentOfferDto>(offer);
        if (truckAccount != null)
        {
            var user = await _db.Users.FindAsync([truckAccount.UserId], cancellationToken);
            dto.TruckAccountName = user?.FullName ?? "";
        }
        return dto;
    }

    public async Task<PaginatedResult<ShipmentOfferDto>> GetMyOffersAsync(Guid truckAccountId, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var query = _db.ShipmentOffers
            .Where(o => o.TruckAccountId == truckAccountId)
            .OrderByDescending(o => o.CreatedAt);

        var total = await query.CountAsync(cancellationToken);

        var offers = await query
            .Skip((pagination.PageNumber - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        var requestIds = offers.Select(o => o.ShipmentRequestId).Distinct().ToList();
        var requests = await _db.ShipmentRequests
            .AsNoTracking()
            .Where(r => requestIds.Contains(r.Id))
            .ToListAsync(cancellationToken);
        var requestLookup = requests.ToDictionary(r => r.Id);

        var regionIds = requests.SelectMany(r => new[] { r.PickupRegionId, r.DropoffRegionId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var regions = regionIds.Any() ? await _db.Regions.Where(reg => regionIds.Contains(reg.Id)).ToDictionaryAsync(reg => reg.Id, cancellationToken) : new Dictionary<Guid, Region>();

        var truckAccount = await _db.TruckAccounts.FindAsync([truckAccountId], cancellationToken);
        var user = truckAccount != null ? await _db.Users.FindAsync([truckAccount.UserId], cancellationToken) : null;
        var truckAccountName = user?.FullName ?? "";

        var items = offers.Select(o =>
        {
            var dto = _mapper.Map<ShipmentOfferDto>(o);
            dto.TruckAccountName = truckAccountName;
            dto.TruckAccountImage = truckAccount?.AvatarUrl;
            var req = requestLookup.GetValueOrDefault(o.ShipmentRequestId);
            if (req != null)
            {
                dto.PickupLocation = req.PickupLocation;
                dto.DropoffLocation = req.DropoffLocation;
                dto.ReferenceNumber = req.RequestNumber;
                dto.ParcelType = req.ParcelType;
                dto.PickupRegion = req.PickupRegionId.HasValue && regions.TryGetValue(req.PickupRegionId.Value, out var pr) ? pr.NameEn : null;
                dto.DropoffRegion = req.DropoffRegionId.HasValue && regions.TryGetValue(req.DropoffRegionId.Value, out var dr) ? dr.NameEn : null;
            }
            return dto;
        }).ToList();

        return new PaginatedResult<ShipmentOfferDto>
        {
            Items = items,
            PageNumber = pagination.PageNumber,
            PageSize = pagination.PageSize,
            TotalCount = total
        };
    }

    public async Task<ShipmentDetailsDto> AssignSelfAsDriverAsync(Guid truckAccountId, Guid shipmentId, Guid truckId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null)
            throw new KeyNotFoundException("Shipment not found.");

        var acceptedOffer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
        if (acceptedOffer == null || acceptedOffer.TruckAccountId != truckAccountId)
            throw new UnauthorizedAccessException("Shipment not found or not assigned to this truck account.");
        if (shipment.Status != ShipmentStatus.AwaitingDriver)
            throw new InvalidOperationException("Driver can only be assigned when shipment is awaiting driver.");

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
                CreatedAt = DateTime.UtcNow
            };
            _db.DriverProfiles.Add(driverProfile);
            await _db.SaveChangesAsync(cancellationToken);
        }

        shipment.DriverProfileId = driverProfile.Id;
        shipment.Status = ShipmentStatus.Ready;
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildShipmentDetailsDtoAsync(shipment.Id, cancellationToken);
    }

    public async Task<ShipmentDetailsDto> AssignDriverAsync(Guid truckAccountId, Guid shipmentId, Guid driverProfileId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null)
            throw new KeyNotFoundException("Shipment not found.");

        var offer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
        if (offer == null || offer.TruckAccountId != truckAccountId)
            throw new UnauthorizedAccessException("Shipment not found or not assigned to this truck account.");

        // Allow assigning when no driver yet (AwaitingDriver) OR when re-assigning after a previous decline/timeout (also AwaitingDriver).
        if (shipment.Status != ShipmentStatus.AwaitingDriver)
            throw new InvalidOperationException("Driver can only be assigned when shipment is awaiting driver.");

        var driver = await _db.DriverProfiles.FindAsync([driverProfileId], cancellationToken);
        if (driver == null)
            throw new KeyNotFoundException("Driver not found.");

        shipment.DriverProfileId = driverProfileId;
        shipment.Status = ShipmentStatus.PendingDriverAcceptance;
        shipment.AssignedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        // Notify the driver. Don't fail the main flow if notification fails.
        if (driver.UserId.HasValue)
        {
            try
            {
                var data = System.Text.Json.JsonSerializer.Serialize(new { shipmentId = shipment.Id });
                await _notificationService.SendNotificationAsync(
                    driver.UserId.Value,
                    "تم تعيينك على شحنة",
                    "اضغط للقبول أو الرفض. ستلغى الشحنة تلقائياً بعد 15 دقيقة في حال عدم الرد.",
                    data,
                    true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify driver (user {DriverUserId}) of assignment to shipment {ShipmentId}", driver.UserId.Value, shipment.Id);
            }
        }

        return await BuildShipmentDetailsDtoAsync(shipment.Id, cancellationToken);
    }

    public async Task<DriverInvitationDto> GenerateDriverInvitationAsync(Guid truckAccountId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null)
            throw new KeyNotFoundException("Shipment not found.");

        var offer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
        if (offer == null || offer.TruckAccountId != truckAccountId)
            throw new UnauthorizedAccessException("Shipment not found or not owned by this account.");

        var now = DateTime.UtcNow;
        var existingInvitation = await _db.DriverInvitations
            .AnyAsync(i => i.ShipmentId == shipmentId && !i.IsUsed && i.ExpiresAt > now, cancellationToken);
        if (existingInvitation)
            throw new InvalidOperationException("An active driver invitation already exists for this shipment.");

        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).TrimEnd('=').Replace("/", "_").Replace("+", "-");
        var invitation = new DriverInvitation
        {
            Id = Guid.NewGuid(),
            ShipmentId = shipmentId,
            Token = token,
            ExpiresAt = now.AddHours(24),
            IsUsed = false,
            CreatedAt = now
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

    public async Task<PaginatedResult<DriverSummaryDto>> GetAvailableDriversAsync(string? search, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        IQueryable<DriverProfile> query = _db.DriverProfiles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            // ILike is the case-insensitive LIKE operator on Postgres (Npgsql).
            query = query.Where(d => EF.Functions.ILike(d.FullName, pattern));
        }

        var pageNumber = pagination.PageNumber < 1 ? 1 : pagination.PageNumber;
        var pageSize = pagination.PageSize < 1 ? 10 : Math.Min(pagination.PageSize, 50);

        var total = await query.CountAsync(cancellationToken);
        var drivers = await query
            .OrderBy(d => d.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var userIds = drivers.Where(d => d.UserId.HasValue).Select(d => d.UserId!.Value).Distinct().ToList();
        var users = userIds.Count > 0
            ? await _db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken)
            : new Dictionary<Guid, HM.Domain.Entities.User>();

        var items = drivers.Select(d => new DriverSummaryDto
        {
            Id = d.Id,
            FullName = d.FullName,
            PhoneNumber = d.UserId.HasValue && users.TryGetValue(d.UserId.Value, out var u) ? u.PhoneNumber : null,
            AvatarUrl = d.AvatarUrl,
            IsVerified = d.IsVerified,
            HasNationalId = !string.IsNullOrEmpty(d.NationalIdFrontImageUrl) && !string.IsNullOrEmpty(d.NationalIdBackImageUrl),
            CreatedAt = d.CreatedAt
        }).ToList();

        return new PaginatedResult<DriverSummaryDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
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
                ShipmentRequestId = s.ShipmentRequestId,
                ShipmentId = s.Id,
                PickupLocation = req?.PickupLocation ?? "",
                DropoffLocation = req?.DropoffLocation ?? "",
                CargoDescription = req?.CargoDescription ?? "",
                RequiredTruckType = req?.RequiredTruckType,
                EstimatedWeight = req?.EstimatedWeightTon ?? 0,
                EstimatedWeightTon = req?.EstimatedWeightTon ?? 0,
                OffersCount = 1,
                Status = s.Status,
                Price = off?.Price ?? 0,
                DriverName = driver?.FullName,
                TruckPlateNumber = truck?.PlateNumber,
                CreatedAt = req?.CreatedAt ?? DateTime.UtcNow
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

    public async Task<ShipmentQrPayloadDto> GetShipmentQrPayloadAsync(Guid truckAccountId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null)
            throw new KeyNotFoundException("Shipment not found.");

        var offer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
        if (offer == null || offer.TruckAccountId != truckAccountId)
            throw new UnauthorizedAccessException("Shipment not found or not assigned to this truck account.");

        var trackingUrl = $"/track/{shipmentId}";
        var payload = System.Text.Json.JsonSerializer.Serialize(new { shipmentId, trackingUrl });
        return new ShipmentQrPayloadDto { Payload = payload };
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
            EstimatedWeight = request?.EstimatedWeightTon ?? 0,
            Notes = request?.Notes,
            Price = offer?.Price ?? 0,
            TruckPlateNumber = truck?.PlateNumber ?? "",
            DriverName = driverName,
            Status = shipment.Status,
            StartedAt = shipment.StartedAt,
            CompletedAt = shipment.CompletedAt
        };
    }

    public async Task<TruckDto> CreateTruckAsync(Guid truckAccountId, CreateTruckRequest request, CancellationToken cancellationToken = default)
    {
        var truck = new Truck
        {
            Id = Guid.NewGuid(),
            TruckAccountId = truckAccountId,
            TruckType = request.TruckType,
            BodyType = request.BodyType,
            MaxWeight = request.MaxWeight,
            PlateNumber = request.PlateNumber.Trim(),
            IsActive = true
        };
        _db.Trucks.Add(truck);
        await _db.SaveChangesAsync(cancellationToken);
        return new TruckDto
        {
            Id = truck.Id,
            TruckAccountId = truck.TruckAccountId,
            TruckType = truck.TruckType,
            MaxWeight = truck.MaxWeight,
            PlateNumber = truck.PlateNumber,
            IsActive = truck.IsActive
        };
    }

    public async Task<IReadOnlyList<TruckDto>> GetMyTrucksAsync(Guid truckAccountId, CancellationToken cancellationToken = default)
    {
        var trucks = await _db.Trucks
            .AsNoTracking()
            .Where(t => t.TruckAccountId == truckAccountId)
            .OrderBy(t => t.PlateNumber)
            .ToListAsync(cancellationToken);
        return trucks.Select(t => new TruckDto
        {
            Id = t.Id,
            TruckAccountId = t.TruckAccountId,
            TruckType = t.TruckType,
            MaxWeight = t.MaxWeight,
            PlateNumber = t.PlateNumber,
            IsActive = t.IsActive
        }).ToList();
    }
}
