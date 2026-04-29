using System.Text.Json;
using AutoMapper;
using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.Models;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HM.Infrastructure.Services;

/// <summary>
/// Merchant operations: profile, shipment requests (wizard), offers, accept/cancel, tracking.
/// </summary>
public sealed class MerchantService : IMerchantService
{
    private static readonly Random Rng = new();
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly ILogger<MerchantService> _logger;

    public MerchantService(IApplicationDbContext db, IMapper mapper, INotificationService notificationService, ILogger<MerchantService> logger)
    {
        _db = db;
        _mapper = mapper;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<MerchantProfileResponse> GetMyProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.MerchantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null)
            throw new KeyNotFoundException("Merchant profile not found.");

        var user = await _db.Users.FindAsync([userId], cancellationToken);
        if (user == null)
            throw new KeyNotFoundException("User not found.");

        return new MerchantProfileResponse
        {
            Id = profile.Id,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = profile.AvatarUrl,
            CreatedAt = profile.CreatedAt
        };
    }

    public async Task<MerchantProfileResponse> UpdateMyProfileAsync(Guid userId, UpdateMerchantProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _db.MerchantProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null)
            throw new KeyNotFoundException("Merchant profile not found.");

        var user = await _db.Users.FindAsync([userId], cancellationToken);
        if (user == null)
            throw new KeyNotFoundException("User not found.");

        if (string.IsNullOrWhiteSpace(request.FullName))
            throw new InvalidOperationException("Full name is required.");

        user.FullName = request.FullName.Trim();
        if (request.AvatarUrl != null)
            profile.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        return new MerchantProfileResponse
        {
            Id = profile.Id,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            AvatarUrl = profile.AvatarUrl,
            CreatedAt = profile.CreatedAt
        };
    }

    public async Task<ShipmentRequestDetailsResponse> CreateShipmentRequestAsync(Guid userId, CreateShipmentRequestRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _db.MerchantProfiles
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null)
            throw new KeyNotFoundException("Merchant profile not found.");

        ValidateCreateRequest(request);

        var cargoDesc = (request.ParcelDescription ?? request.ParcelType ?? "").Trim();
        if (string.IsNullOrEmpty(cargoDesc))
            cargoDesc = "Parcel";

        var entity = new ShipmentRequest
        {
            Id = Guid.NewGuid(),
            MerchantProfileId = profile.Id,
            RequestNumber = await GenerateUniqueRequestNumberAsync(cancellationToken),
            RequiredTruckType = request.TruckType,
            RequiredTruckBodyType = request.TruckBodyType,

            PickupGovernorateId = request.PickupGovernorateId,
            PickupRegionId = request.PickupRegionId,
            PickupLocation = request.PickupAddressText.Trim(),
            PickupArea = request.PickupArea?.Trim(),

            DropoffGovernorateId = request.DropoffGovernorateId,
            DropoffRegionId = request.DropoffRegionId,
            DropoffLocation = request.DropoffAddressText.Trim(),
            DropoffArea = request.DropoffArea?.Trim(),

            SenderName = request.SenderName.Trim(),
            SenderPhone = request.SenderPhone.Trim(),
            ReceiverPhoneNumber = request.ReceiverPhoneNumber?.Trim(),

            CargoDescription = cargoDesc,
            ParcelType = request.ParcelType?.Trim(),
            EstimatedWeightTon = request.ParcelWeightTon,
            ParcelSize = request.ParcelSize?.Trim(),
            ParcelCount = request.ParcelCount,
            IsFragile = request.IsFragile,

            DeliveryDate = request.DeliveryDate,
            DeliveryTimeFrom = request.DeliveryTimeFrom,
            DeliveryTimeTo = request.DeliveryTimeTo,

            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes?.Trim(),
            Status = ShipmentRequestStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        _db.ShipmentRequests.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyMerchantRequestSubmittedAsync(userId, cancellationToken);
        await NotifyDriversNewRequestAsync(entity.Id, cancellationToken);

        return await BuildDetailsResponseAsync(profile.Id, entity.Id, cancellationToken);
    }

    public async Task<PaginatedResult<ShipmentRequestSummaryResponse>> GetMyShipmentRequestsAsync(Guid userId, GetMerchantShipmentRequestsQuery query, CancellationToken cancellationToken = default)
    {
        var profileId = await ResolveMerchantProfileIdAsync(userId, cancellationToken);

        var q = _db.ShipmentRequests
            .AsNoTracking()
            .Where(r => r.MerchantProfileId == profileId);

        if (query.Status.HasValue)
            q = q.Where(r => r.Status == query.Status.Value);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(r => r.RequestNumber.Contains(search));
        }

        if (query.DateFrom.HasValue)
            q = q.Where(r => r.CreatedAt >= query.DateFrom.Value.ToDateTime(TimeOnly.MinValue));
        if (query.DateTo.HasValue)
            q = q.Where(r => r.CreatedAt <= query.DateTo.Value.ToDateTime(TimeOnly.MaxValue));

        q = q.OrderByDescending(r => r.CreatedAt);

        var total = await q.CountAsync(cancellationToken);
        var pageNumber = query.PageNumber < 1 ? 1 : query.PageNumber;
        var pageSize = query.PageSize < 1 ? 10 : Math.Min(query.PageSize, 50);

        var items = await q
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var requestIds = items.Select(i => i.Id).ToList();
        var offerCounts = await _db.ShipmentOffers
            .Where(o => requestIds.Contains(o.ShipmentRequestId))
            .GroupBy(o => o.ShipmentRequestId)
            .Select(g => new { RequestId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var countLookup = offerCounts.ToDictionary(x => x.RequestId, x => x.Count);

        var acceptedOffers = await _db.Shipments
            .Where(s => requestIds.Contains(s.ShipmentRequestId))
            .Join(_db.ShipmentOffers, s => s.AcceptedOfferId, o => o.Id, (s, o) => new { s.ShipmentRequestId, o.Price })
            .ToDictionaryAsync(x => x.ShipmentRequestId, x => x.Price, cancellationToken);

        var regionIds = items.SelectMany(r => new[] { r.PickupRegionId, r.DropoffRegionId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var governorateIds = items.SelectMany(r => new[] { r.PickupGovernorateId, r.DropoffGovernorateId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var regions = regionIds.Any() ? await _db.Regions.Where(reg => regionIds.Contains(reg.Id)).ToDictionaryAsync(reg => reg.Id, cancellationToken) : new Dictionary<Guid, Region>();
        var governorates = governorateIds.Any() ? await _db.Governorates.Where(g => governorateIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, cancellationToken) : new Dictionary<Guid, Governorate>();

        var summaries = items.Select(r =>
        {
            var from = r.DeliveryTimeFrom;
            var to = r.DeliveryTimeTo;
            var timeWindow = (from.HasValue || to.HasValue)
                ? $"{(from.HasValue ? from.Value.ToString("HH\\:mm") : "?")} - {(to.HasValue ? to.Value.ToString("HH\\:mm") : "?")}"
                : "";

            return new ShipmentRequestSummaryResponse
            {
                Id = r.Id,
                RequestNumber = r.RequestNumber,
                Status = r.Status,
                TruckType = r.RequiredTruckType,
                CreatedAt = r.CreatedAt,
                PickupGovernorate = r.PickupGovernorateId.HasValue && governorates.TryGetValue(r.PickupGovernorateId.Value, out var pg) ? pg.NameEn : null,
                PickupRegion = r.PickupRegionId.HasValue && regions.TryGetValue(r.PickupRegionId.Value, out var pr) ? pr.NameEn : null,
                PickupAreaOrText = r.PickupArea ?? r.PickupLocation,
                DropoffAreaOrText = r.DropoffArea ?? r.DropoffLocation,
                DeliveryDate = r.DeliveryDate,
                DeliveryTimeWindow = timeWindow,
                OffersCount = countLookup.GetValueOrDefault(r.Id, 0),
                AmountDue = acceptedOffers.TryGetValue(r.Id, out var price) ? price : 0
            };
        }).ToList();

        return new PaginatedResult<ShipmentRequestSummaryResponse>
        {
            Items = summaries,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };
    }

    public async Task<ShipmentRequestDetailsResponse> GetMyShipmentRequestDetailsAsync(Guid userId, Guid shipmentRequestId, CancellationToken cancellationToken = default)
    {
        var profileId = await ResolveMerchantProfileIdAsync(userId, cancellationToken);
        return await BuildDetailsResponseAsync(profileId, shipmentRequestId, cancellationToken);
    }

    public async Task<IReadOnlyList<ShipmentOfferResponse>> GetOffersForMyRequestAsync(Guid userId, Guid shipmentRequestId, CancellationToken cancellationToken = default)
    {
        var profileId = await ResolveMerchantProfileIdAsync(userId, cancellationToken);

        var request = await _db.ShipmentRequests.FindAsync([shipmentRequestId], cancellationToken);
        if (request == null || request.MerchantProfileId != profileId)
            throw new KeyNotFoundException("Shipment request not found.");

        var offers = await _db.ShipmentOffers
            .AsNoTracking()
            .Where(o => o.ShipmentRequestId == shipmentRequestId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        var truckAccountIds = offers.Select(o => o.TruckAccountId).Distinct().ToList();
        var truckAccounts = await _db.TruckAccounts
            .AsNoTracking()
            .Where(t => truckAccountIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);
        var userIds = truckAccounts.Values.Select(t => t.UserId).Distinct().ToList();
        var users = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        var truckCounts = await _db.Trucks
            .Where(t => truckAccountIds.Contains(t.TruckAccountId))
            .GroupBy(t => t.TruckAccountId)
            .Select(g => new { TruckAccountId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);
        var truckCountLookup = truckCounts.ToDictionary(x => x.TruckAccountId, x => x.Count);

        var firstTruckPerAccount = await _db.Trucks
            .AsNoTracking()
            .Where(t => truckAccountIds.Contains(t.TruckAccountId))
            .GroupBy(t => t.TruckAccountId)
            .Select(g => g.OrderBy(t => t.PlateNumber).First())
            .ToDictionaryAsync(t => t.TruckAccountId, cancellationToken);

        return offers.Select(o =>
        {
            var acc = truckAccounts.GetValueOrDefault(o.TruckAccountId);
            var user = acc != null ? users.GetValueOrDefault(acc.UserId) : null;
            var truck = firstTruckPerAccount.GetValueOrDefault(o.TruckAccountId);
            return new ShipmentOfferResponse
            {
                OfferId = o.Id,
                TruckAccountId = o.TruckAccountId,
                Price = o.Price,
                Currency = "SAR",
                Notes = o.Notes,
                CreatedAt = o.CreatedAt,
                TruckAccountName = user?.FullName ?? "",
                TrucksCount = truckCountLookup.GetValueOrDefault(o.TruckAccountId, 0),
                Status = o.Status,
                TruckAccountImage = acc?.AvatarUrl,
                TruckSize = truck != null ? truck.TruckType.ToString() : null,
                TruckType = truck?.BodyType != null ? truck.BodyType.ToString() : null,
                ParcelWeightTon = request.EstimatedWeightTon
            };
        }).ToList();
    }

    public async Task<ShipmentRequestDetailsResponse> AcceptOfferAsync(Guid userId, Guid shipmentRequestId, Guid offerId, CancellationToken cancellationToken = default)
    {
        var profileId = await ResolveMerchantProfileIdAsync(userId, cancellationToken);

        var offer = await _db.ShipmentOffers.FirstOrDefaultAsync(o => o.Id == offerId, cancellationToken);
        if (offer == null)
            throw new KeyNotFoundException("Offer not found.");
        if (offer.ShipmentRequestId != shipmentRequestId)
            throw new InvalidOperationException("Offer does not belong to this request.");
        if (offer.Status != ShipmentOfferStatus.Pending)
            throw new InvalidOperationException("Offer is no longer pending.");

        var request = await _db.ShipmentRequests.FindAsync([shipmentRequestId], cancellationToken);
        if (request == null || request.MerchantProfileId != profileId)
            throw new KeyNotFoundException("Shipment request not found.");
        if (request.Status != ShipmentRequestStatus.Open)
            throw new InvalidOperationException("Request is no longer open for acceptance.");

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

        await NotifyDriverOfferAcceptedAsync(offer.TruckAccountId, request.Id, offer.Id, cancellationToken);

        return await BuildDetailsResponseAsync(profileId, request.Id, cancellationToken);
    }

    public async Task RejectOfferAsync(Guid userId, Guid shipmentRequestId, Guid offerId, CancellationToken cancellationToken = default)
    {
        var profileId = await ResolveMerchantProfileIdAsync(userId, cancellationToken);

        var request = await _db.ShipmentRequests.FindAsync([shipmentRequestId], cancellationToken);
        if (request == null || request.MerchantProfileId != profileId)
            throw new KeyNotFoundException("Shipment request not found.");

        var offer = await _db.ShipmentOffers.FirstOrDefaultAsync(o => o.Id == offerId, cancellationToken);
        if (offer == null || offer.ShipmentRequestId != shipmentRequestId)
            throw new KeyNotFoundException("Offer not found.");
        if (offer.Status != ShipmentOfferStatus.Pending)
            throw new InvalidOperationException("Offer is no longer pending.");

        offer.Status = ShipmentOfferStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task CancelShipmentRequestAsync(Guid userId, Guid shipmentRequestId, CancellationToken cancellationToken = default)
    {
        var profileId = await ResolveMerchantProfileIdAsync(userId, cancellationToken);

        var request = await _db.ShipmentRequests.FindAsync([shipmentRequestId], cancellationToken);
        if (request == null || request.MerchantProfileId != profileId)
            throw new KeyNotFoundException("Shipment request not found.");

        if (request.Status != ShipmentRequestStatus.Open && request.Status != ShipmentRequestStatus.Draft)
            throw new InvalidOperationException("Only open or draft requests can be cancelled.");

        request.Status = ShipmentRequestStatus.Cancelled;
        var pendingOffers = await _db.ShipmentOffers
            .Where(o => o.ShipmentRequestId == shipmentRequestId && o.Status == ShipmentOfferStatus.Pending)
            .ToListAsync(cancellationToken);
        foreach (var o in pendingOffers)
            o.Status = ShipmentOfferStatus.Rejected;
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<ShipmentTrackingResponse> GetTrackingAsync(Guid userId, Guid shipmentIdOrRequestId, CancellationToken cancellationToken = default)
    {
        var profileId = await ResolveMerchantProfileIdAsync(userId, cancellationToken);

        Shipment? shipment = null;
        var byShipment = await _db.Shipments.FindAsync([shipmentIdOrRequestId], cancellationToken);
        if (byShipment != null)
            shipment = byShipment;
        else
        {
            var byRequest = await _db.Shipments
                .FirstOrDefaultAsync(s => s.ShipmentRequestId == shipmentIdOrRequestId, cancellationToken);
            shipment = byRequest;
        }

        if (shipment == null)
            throw new KeyNotFoundException("Shipment not found.");

        var request = await _db.ShipmentRequests.FindAsync([shipment.ShipmentRequestId], cancellationToken);
        if (request == null || request.MerchantProfileId != profileId)
            throw new UnauthorizedAccessException("Shipment not found.");

        string? driverName = null;
        string? driverPhone = null;
        if (shipment.DriverProfileId.HasValue)
        {
            var driver = await _db.DriverProfiles.FindAsync([shipment.DriverProfileId.Value], cancellationToken);
            if (driver != null)
            {
                driverName = driver.FullName;
                var driverUser = driver.UserId.HasValue ? await _db.Users.FindAsync([driver.UserId.Value], cancellationToken) : null;
                driverPhone = driverUser?.PhoneNumber;
            }
        }

        var truck = await _db.Trucks.FindAsync([shipment.TruckId], cancellationToken);

        return new ShipmentTrackingResponse
        {
            ShipmentId = shipment.Id,
            Status = shipment.Status,
            CurrentLat = shipment.CurrentLat,
            CurrentLng = shipment.CurrentLng,
            RoutePolyline = null,
            LastUpdatedAt = shipment.LocationUpdatedAt ?? shipment.StartedAt ?? shipment.CompletedAt,
            DriverName = driverName,
            DriverPhone = driverPhone,
            TruckPlateNumber = truck?.PlateNumber,
            TruckType = truck?.TruckType
        };
    }

    private async Task<Guid> ResolveMerchantProfileIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await _db.MerchantProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile == null)
            throw new KeyNotFoundException("Merchant profile not found.");
        return profile.Id;
    }

    private static void ValidateCreateRequest(CreateShipmentRequestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SenderPhone))
            throw new InvalidOperationException("Sender phone is required.");
        if (string.IsNullOrWhiteSpace(request.PickupAddressText))
            throw new InvalidOperationException("Pickup address is required.");
        if (string.IsNullOrWhiteSpace(request.DropoffAddressText))
            throw new InvalidOperationException("Dropoff address is required.");
        if (request.ParcelCount < 1)
            throw new InvalidOperationException("Parcel count must be at least 1.");
        if (request.ParcelWeightTon <= 0)
            throw new InvalidOperationException("Parcel weight (ton) must be greater than 0.");
        if (request.DeliveryDate < DateOnly.FromDateTime(DateTime.UtcNow))
            throw new InvalidOperationException("Delivery date cannot be in the past.");
        if (request.DeliveryTimeFrom.HasValue && request.DeliveryTimeTo.HasValue && request.DeliveryTimeFrom > request.DeliveryTimeTo)
            throw new InvalidOperationException("Delivery time 'from' must be before 'to'.");
        if (!Enum.IsDefined(typeof(PaymentMethod), request.PaymentMethod))
            throw new InvalidOperationException("Invalid payment method.");
    }

    private async Task<string> GenerateUniqueRequestNumberAsync(CancellationToken cancellationToken)
    {
        for (var i = 0; i < 10; i++)
        {
            var code = "HM" + Rng.Next(100000, 999999).ToString();
            var exists = await _db.ShipmentRequests.AnyAsync(r => r.RequestNumber == code, cancellationToken);
            if (!exists)
                return code;
        }
        return "HM" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }

    private async Task<ShipmentRequestDetailsResponse> BuildDetailsResponseAsync(Guid merchantProfileId, Guid shipmentRequestId, CancellationToken cancellationToken)
    {
        var request = await _db.ShipmentRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == shipmentRequestId && r.MerchantProfileId == merchantProfileId, cancellationToken);
        if (request == null)
            throw new KeyNotFoundException("Shipment request not found.");

        var offersCount = await _db.ShipmentOffers.CountAsync(o => o.ShipmentRequestId == shipmentRequestId, cancellationToken);

        string? pickupRegionName = null;
        string? dropoffRegionName = null;
        if (request.PickupRegionId.HasValue)
            pickupRegionName = await _db.Regions.Where(r => r.Id == request.PickupRegionId.Value).Select(r => r.NameEn).FirstOrDefaultAsync(cancellationToken);
        if (request.DropoffRegionId.HasValue)
            dropoffRegionName = await _db.Regions.Where(r => r.Id == request.DropoffRegionId.Value).Select(r => r.NameEn).FirstOrDefaultAsync(cancellationToken);

        var from = request.DeliveryTimeFrom;
        var to = request.DeliveryTimeTo;
        var deliveryTimeWindow = (from.HasValue || to.HasValue)
            ? $"{(from.HasValue ? from.Value.ToString("HH\\:mm") : "?")} - {(to.HasValue ? to.Value.ToString("HH\\:mm") : "?")}"
            : "";

        AcceptedOfferSummary? acceptedOffer = null;
        AssignedDriverSummary? assignedDriver = null;
        string? driverImage = null;

        var shipment = await _db.Shipments
            .FirstOrDefaultAsync(s => s.ShipmentRequestId == shipmentRequestId, cancellationToken);
        if (shipment != null)
        {
            var offer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
            string? truckAccountName = null;
            if (offer != null)
            {
                var truckAcc = await _db.TruckAccounts.FindAsync([offer.TruckAccountId], cancellationToken);
                if (truckAcc != null)
                {
                    var user = await _db.Users.FindAsync([truckAcc.UserId], cancellationToken);
                    truckAccountName = user?.FullName;
                }
            }
            acceptedOffer = new AcceptedOfferSummary
            {
                OfferId = shipment.AcceptedOfferId,
                Price = offer?.Price ?? 0,
                TruckAccountName = truckAccountName
            };

            var truck = await _db.Trucks.FindAsync([shipment.TruckId], cancellationToken);
            if (shipment.DriverProfileId.HasValue)
            {
                var driver = await _db.DriverProfiles.FindAsync([shipment.DriverProfileId.Value], cancellationToken);
                driverImage = driver?.AvatarUrl;
                string? driverPhone = null;
                if (driver?.UserId != null)
                {
                    var du = await _db.Users.FindAsync([driver.UserId.Value], cancellationToken);
                    driverPhone = du?.PhoneNumber;
                }
                assignedDriver = new AssignedDriverSummary
                {
                    DriverName = driver?.FullName,
                    DriverPhone = driverPhone,
                    TruckPlateNumber = truck?.PlateNumber
                };
            }
            else
            {
                assignedDriver = new AssignedDriverSummary { TruckPlateNumber = truck?.PlateNumber };
            }
        }

        var (estimatedTime, distanceKm) = EstimateTimeAndDistance(request.PickupGovernorateId, request.DropoffGovernorateId, request.PickupRegionId, request.DropoffRegionId);

        return new ShipmentRequestDetailsResponse
        {
            Id = request.Id,
            RequestNumber = request.RequestNumber,
            Status = request.Status,
            TruckType = request.RequiredTruckType,
            CreatedAt = request.CreatedAt,
            PickupAddressText = request.PickupLocation,
            PickupArea = request.PickupArea,
            DropoffAddressText = request.DropoffLocation,
            DropoffArea = request.DropoffArea,
            SenderName = request.SenderName,
            SenderPhone = request.SenderPhone,
            ParcelDescription = request.CargoDescription,
            ParcelType = request.ParcelType,
            ParcelWeightTon = request.EstimatedWeightTon,
            ParcelSize = request.ParcelSize,
            ParcelCount = request.ParcelCount,
            DeliveryDate = request.DeliveryDate,
            DeliveryTimeFrom = request.DeliveryTimeFrom,
            DeliveryTimeTo = request.DeliveryTimeTo,
            DeliveryTimeWindow = deliveryTimeWindow,
            PaymentMethod = request.PaymentMethod,
            Notes = request.Notes,
            OffersCount = offersCount,
            AcceptedOffer = acceptedOffer,
            AssignedDriver = assignedDriver,
            EstimatedTime = estimatedTime,
            DistanceKm = distanceKm,
            PickupRegion = pickupRegionName,
            DropoffRegion = dropoffRegionName,
            DriverImage = driverImage
        };
    }

    private static (string? estimatedTime, decimal? distanceKm) EstimateTimeAndDistance(Guid? pickupGovId, Guid? dropoffGovId, Guid? pickupRegionId, Guid? dropoffRegionId)
    {
        if (!pickupGovId.HasValue || !dropoffGovId.HasValue) return (null, null);
        var sameGovernorate = pickupGovId == dropoffGovId;
        var sameRegion = pickupRegionId.HasValue && dropoffRegionId.HasValue && pickupRegionId == dropoffRegionId;
        if (sameRegion) return ("1-2 hours", 25m);
        if (sameGovernorate) return ("2-4 hours", 80m);
        return ("4-8 hours", 250m);
    }

    private async Task NotifyMerchantRequestSubmittedAsync(Guid merchantUserId, CancellationToken cancellationToken)
    {
        try
        {
            await _notificationService.SendNotificationAsync(
                merchantUserId,
                "طلبك قيد التقييم",
                "طلبك قيد التقييم سيصلك إشعار فور قبوله.",
                null,
                true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify merchant {MerchantUserId} of submitted shipment request; continuing without push", merchantUserId);
        }
    }

    private async Task NotifyDriversNewRequestAsync(Guid shipmentRequestId, CancellationToken cancellationToken)
    {
        try
        {
            var truckAccountIds = await _db.Trucks
                .Where(t => t.IsActive)
                .Select(t => t.TruckAccountId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var driverUserIds = await _db.TruckAccounts
                .Where(ta => truckAccountIds.Contains(ta.Id))
                .Select(ta => ta.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            var data = JsonSerializer.Serialize(new { shipmentRequestId });
            foreach (var driverUserId in driverUserIds)
            {
                try
                {
                    await _notificationService.SendNotificationAsync(
                        driverUserId,
                        "طلب جديد",
                        "يوجد طلب مستعجل اضغط لكتابة عرض سعر.",
                        data,
                        true,
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to notify driver {DriverUserId} of new shipment request {ShipmentRequestId}; continuing with other drivers", driverUserId, shipmentRequestId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fan out new-shipment-request notifications for {ShipmentRequestId}", shipmentRequestId);
        }
    }

    private async Task NotifyDriverOfferAcceptedAsync(Guid truckAccountId, Guid shipmentRequestId, Guid offerId, CancellationToken cancellationToken)
    {
        try
        {
            var truckAccount = await _db.TruckAccounts.FindAsync([truckAccountId], cancellationToken);
            if (truckAccount == null) return;
            var data = JsonSerializer.Serialize(new { shipmentRequestId, offerId });
            await _notificationService.SendNotificationAsync(
                truckAccount.UserId,
                "تم قبول عرضك",
                "تم قبول عرضك على الطلب.",
                data,
                true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify truck account {TruckAccountId} of accepted offer {OfferId} on request {ShipmentRequestId}", truckAccountId, offerId, shipmentRequestId);
        }
    }
}
