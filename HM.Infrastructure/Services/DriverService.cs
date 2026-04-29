using System.Text.Json;
using AutoMapper;
using HM.Application.Common.DTOs.Driver;
using HM.Application.Common.DTOs.Shipment;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace HM.Infrastructure.Services;

/// <summary>
/// Driver operations: upload national ID, start/pause/complete shipment, get assigned shipment (no price).
/// </summary>
public sealed class DriverService : IDriverService
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DriverService> _logger;

    public DriverService(IApplicationDbContext db, IMapper mapper, INotificationService notificationService, ILogger<DriverService> logger)
    {
        _db = db;
        _mapper = mapper;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<DriverProfileDto> GetMyProfileAsync(Guid driverUserId, CancellationToken cancellationToken = default)
    {
        var profile = await _db.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == driverUserId, cancellationToken);
        if (profile == null)
            throw new KeyNotFoundException("Driver profile not found.");

        var user = await _db.Users.FindAsync([driverUserId], cancellationToken);
        var dto = _mapper.Map<DriverProfileDto>(profile);
        dto.PhoneNumber = user?.PhoneNumber;
        dto.HasNationalId = !string.IsNullOrEmpty(profile.NationalIdFrontImageUrl) && !string.IsNullOrEmpty(profile.NationalIdBackImageUrl);
        return dto;
    }

    public async Task<DriverProfileDto> UpdateMyProfileAsync(Guid driverUserId, UpdateDriverProfileRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _db.DriverProfiles.FirstOrDefaultAsync(p => p.UserId == driverUserId, cancellationToken);
        if (profile == null)
            throw new KeyNotFoundException("Driver profile not found.");

        if (!string.IsNullOrWhiteSpace(request.FullName))
            profile.FullName = request.FullName.Trim();
        if (request.AvatarUrl != null)
            profile.AvatarUrl = string.IsNullOrWhiteSpace(request.AvatarUrl) ? null : request.AvatarUrl.Trim();
        if (request.NationalIdFrontImageUrl != null)
            profile.NationalIdFrontImageUrl = string.IsNullOrWhiteSpace(request.NationalIdFrontImageUrl) ? null : request.NationalIdFrontImageUrl.Trim();
        if (request.NationalIdBackImageUrl != null)
            profile.NationalIdBackImageUrl = string.IsNullOrWhiteSpace(request.NationalIdBackImageUrl) ? null : request.NationalIdBackImageUrl.Trim();

        var user = await _db.Users.FindAsync([driverUserId], cancellationToken);
        if (user != null && !string.IsNullOrWhiteSpace(request.PhoneNumber))
            user.PhoneNumber = request.PhoneNumber.Trim();

        await _db.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<DriverProfileDto>(profile);
        dto.PhoneNumber = user?.PhoneNumber;
        dto.HasNationalId = !string.IsNullOrEmpty(profile.NationalIdFrontImageUrl) && !string.IsNullOrEmpty(profile.NationalIdBackImageUrl);
        return dto;
    }

    public async Task<DriverProfileDto> UploadNationalIdAsync(Guid driverProfileId, UploadNationalIdRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _db.DriverProfiles.FindAsync([driverProfileId], cancellationToken);
        if (profile == null)
            throw new InvalidOperationException("Driver profile not found.");

        if (!string.IsNullOrWhiteSpace(request.NationalIdFrontImageUrl))
            profile.NationalIdFrontImageUrl = request.NationalIdFrontImageUrl.Trim();
        if (!string.IsNullOrWhiteSpace(request.NationalIdBackImageUrl))
            profile.NationalIdBackImageUrl = request.NationalIdBackImageUrl.Trim();
        await _db.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<DriverProfileDto>(profile);
        dto.HasNationalId = !string.IsNullOrEmpty(profile.NationalIdFrontImageUrl) && !string.IsNullOrEmpty(profile.NationalIdBackImageUrl);
        return dto;
    }

    public async Task<ShipmentStatusDto> StartShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null || shipment.DriverProfileId != driverProfileId)
            throw new InvalidOperationException("Shipment not found or not assigned to this driver.");
        if (shipment.Status != ShipmentStatus.Ready)
            throw new InvalidOperationException("Shipment is not ready to start.");

        shipment.Status = ShipmentStatus.InTransit;
        shipment.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<ShipmentStatusDto>(shipment);
        dto.UpdatedAt = DateTime.UtcNow;
        return dto;
    }

    public async Task<ShipmentStatusDto> PauseShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null || shipment.DriverProfileId != driverProfileId)
            throw new InvalidOperationException("Shipment not found or not assigned to this driver.");
        if (shipment.Status != ShipmentStatus.InTransit)
            throw new InvalidOperationException("Shipment is not in transit.");

        shipment.Status = ShipmentStatus.Paused;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<ShipmentStatusDto>(shipment);
        dto.UpdatedAt = DateTime.UtcNow;
        return dto;
    }

    public async Task<ShipmentStatusDto> CompleteShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null || shipment.DriverProfileId != driverProfileId)
            throw new InvalidOperationException("Shipment not found or not assigned to this driver.");
        if (shipment.Status != ShipmentStatus.InTransit && shipment.Status != ShipmentStatus.Paused)
            throw new InvalidOperationException("Shipment cannot be completed in current status.");

        shipment.Status = ShipmentStatus.Completed;
        shipment.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyMerchantShipmentDeliveredAsync(shipment.Id, cancellationToken);

        var dto = _mapper.Map<ShipmentStatusDto>(shipment);
        dto.UpdatedAt = DateTime.UtcNow;
        return dto;
    }

    public async Task<AssignedShipmentDto> GetAssignedShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null || shipment.DriverProfileId != driverProfileId)
            throw new InvalidOperationException("Shipment not found or not assigned to this driver.");

        var request = await _db.ShipmentRequests.FindAsync([shipment.ShipmentRequestId], cancellationToken);
        if (request == null)
            throw new InvalidOperationException("Shipment request not found.");

        var truck = await _db.Trucks.FindAsync([shipment.TruckId], cancellationToken);

        var dto = _mapper.Map<AssignedShipmentDto>(shipment);
        dto.PickupLocation = request.PickupLocation;
        dto.DropoffLocation = request.DropoffLocation;
        dto.CargoDescription = request.CargoDescription;
        dto.EstimatedWeight = request.EstimatedWeightTon;
        dto.Notes = request.Notes;
        dto.TruckPlateNumber = truck?.PlateNumber;
        return dto;
    }

    public async Task<DriverShipmentDetailsResponse> AcceptAssignmentAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.PendingDriverAcceptance)
            throw new InvalidOperationException("Assignment is not awaiting your acceptance.");

        shipment.Status = ShipmentStatus.Ready;
        shipment.AssignedAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyTruckAccountOfAssignmentResponseAsync(shipment, request, accepted: true, cancellationToken);

        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    public async Task<DriverShipmentDetailsResponse> RejectAssignmentAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.PendingDriverAcceptance)
            throw new InvalidOperationException("Assignment is not awaiting your acceptance.");

        shipment.Status = ShipmentStatus.AwaitingDriver;
        shipment.DriverProfileId = null;
        shipment.AssignedAt = null;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyTruckAccountOfAssignmentResponseAsync(shipment, request, accepted: false, cancellationToken);

        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    private async Task NotifyTruckAccountOfAssignmentResponseAsync(Shipment shipment, ShipmentRequest request, bool accepted, CancellationToken cancellationToken)
    {
        try
        {
            var offer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
            if (offer == null) return;
            var truckAccount = await _db.TruckAccounts.FindAsync([offer.TruckAccountId], cancellationToken);
            if (truckAccount == null) return;
            var title = accepted ? "قبل السائق الشحنة" : "رفض السائق الشحنة";
            var body = accepted
                ? $"قبل السائق الشحنة رقم {request.RequestNumber} وأصبحت جاهزة للانطلاق."
                : $"رفض السائق الشحنة رقم {request.RequestNumber}. يرجى تعيين سائق آخر.";
            var data = JsonSerializer.Serialize(new { shipmentId = shipment.Id });
            await _notificationService.SendNotificationAsync(
                truckAccount.UserId,
                title,
                body,
                data,
                true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify truck account of driver assignment response (shipment {ShipmentId}, accepted={Accepted})", shipment.Id, accepted);
        }
    }

    public async Task<DriverShipmentDetailsResponse> GetMyShipmentDetailsAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    public async Task<DriverShipmentDetailsResponse> StartTripAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.AwaitingDriver && shipment.Status != ShipmentStatus.Ready)
            throw new InvalidOperationException("Shipment can only be started when it is awaiting driver or ready.");

        shipment.Status = ShipmentStatus.InTransit;
        shipment.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyMerchantStatusChangeAsync(shipment.Id, "بدأ الرحلة", $"بدأ السائق رحلة توصيل طلب رقم {request.RequestNumber}", cancellationToken);

        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    public async Task<DriverShipmentDetailsResponse> MarkArrivedAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.InTransit)
            throw new InvalidOperationException("Shipment must be in transit before marking as arrived.");

        shipment.Status = ShipmentStatus.Arrived;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyMerchantStatusChangeAsync(shipment.Id, "تم الوصول", $"وصل السائق لتسليم طلب رقم {request.RequestNumber}", cancellationToken);

        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    public async Task<DriverShipmentDetailsResponse> MarkReceivedAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.Arrived)
            throw new InvalidOperationException("Shipment must be arrived before marking as received.");

        shipment.Status = ShipmentStatus.Completed;
        shipment.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyMerchantShipmentDeliveredAsync(shipment.Id, cancellationToken);

        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    public async Task<DriverShipmentTrackingResponse> GetMyShipmentTrackingAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, _) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        return new DriverShipmentTrackingResponse
        {
            ShipmentId = shipment.Id,
            Status = shipment.Status,
            CurrentLat = shipment.CurrentLat,
            CurrentLng = shipment.CurrentLng,
            LastUpdatedAt = shipment.LocationUpdatedAt ?? shipment.StartedAt ?? shipment.CompletedAt,
            RoutePolyline = null
        };
    }

    public async Task<LocationUpdateResponse> UpdateLocationAsync(Guid driverUserId, Guid shipmentId, UpdateLocationRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Latitude < -90 || request.Latitude > 90)
            throw new ArgumentException("Latitude must be between -90 and 90.");
        if (request.Longitude < -180 || request.Longitude > 180)
            throw new ArgumentException("Longitude must be between -180 and 180.");

        var (shipment, _) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.InTransit)
            throw new InvalidOperationException("Location updates are only accepted when shipment is in transit.");

        shipment.CurrentLat = request.Latitude;
        shipment.CurrentLng = request.Longitude;
        shipment.LocationUpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return new LocationUpdateResponse
        {
            ShipmentId = shipment.Id,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            UpdatedAt = shipment.LocationUpdatedAt.Value
        };
    }

    public async Task<DriverShipmentDetailsResponse> PauseTripAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.InTransit)
            throw new InvalidOperationException("Shipment must be in transit before it can be paused.");

        shipment.Status = ShipmentStatus.Paused;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyMerchantStatusChangeAsync(shipment.Id, "توقف مؤقت", $"تم إيقاف رحلة طلب رقم {request.RequestNumber} مؤقتا", cancellationToken);

        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    public async Task<DriverShipmentDetailsResponse> ResumeTripAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.Paused)
            throw new InvalidOperationException("Shipment must be paused before it can be resumed.");

        shipment.Status = ShipmentStatus.InTransit;
        await _db.SaveChangesAsync(cancellationToken);

        await NotifyMerchantStatusChangeAsync(shipment.Id, "استئناف الرحلة", $"تم استئناف رحلة طلب رقم {request.RequestNumber}", cancellationToken);

        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    /// <summary>
    /// Loads shipment and request; throws NotFound if shipment missing, Forbidden if not assigned to this driver.
    /// </summary>
    private async Task<(Shipment Shipment, ShipmentRequest Request)> LoadShipmentOwnedByDriverAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken)
    {
        var driverProfileId = await ResolveDriverProfileIdAsync(driverUserId, cancellationToken);

        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null)
            throw new KeyNotFoundException("Shipment not found.");
        if (shipment.DriverProfileId != driverProfileId)
            throw new UnauthorizedAccessException("Shipment is not assigned to you.");

        var request = await _db.ShipmentRequests.FindAsync([shipment.ShipmentRequestId], cancellationToken);
        if (request == null)
            throw new KeyNotFoundException("Shipment request not found.");

        return (shipment, request);
    }

    private async Task<Guid> ResolveDriverProfileIdAsync(Guid driverUserId, CancellationToken cancellationToken)
    {
        var profile = await _db.DriverProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == driverUserId, cancellationToken);
        if (profile == null)
            throw new KeyNotFoundException("Driver profile not found.");
        return profile.Id;
    }

    private async Task<DriverShipmentDetailsResponse> BuildDriverShipmentDetailsAsync(Shipment shipment, ShipmentRequest request, CancellationToken cancellationToken)
    {
        string? merchantName = null;
        string? merchantPhone = null;
        var merchantProfile = await _db.MerchantProfiles.FindAsync([request.MerchantProfileId], cancellationToken);
        if (merchantProfile != null)
        {
            var merchantUser = await _db.Users.FindAsync([merchantProfile.UserId], cancellationToken);
            merchantName = merchantUser?.FullName;
            merchantPhone = merchantUser?.PhoneNumber;
        }

        decimal? offerPrice = null;
        var offer = await _db.ShipmentOffers.FindAsync([shipment.AcceptedOfferId], cancellationToken);
        if (offer != null)
            offerPrice = offer.Price;

        var from = request.DeliveryTimeFrom;
        var to = request.DeliveryTimeTo;
        var deliveryTimeWindow = (from.HasValue || to.HasValue)
            ? $"{(from.HasValue ? from.Value.ToString("HH\\:mm") : "?")} - {(to.HasValue ? to.Value.ToString("HH\\:mm") : "?")}"
            : "";

        return new DriverShipmentDetailsResponse
        {
            ShipmentId = shipment.Id,
            RequestNumber = request.RequestNumber,
            Status = shipment.Status,
            PickupAddressText = request.PickupLocation,
            PickupLat = null,
            PickupLng = null,
            DropoffAddressText = request.DropoffLocation,
            DropoffLat = null,
            DropoffLng = null,
            MerchantName = merchantName,
            MerchantPhone = merchantPhone,
            RecipientName = request.SenderName,
            RecipientPhone = request.SenderPhone,
            ParcelDescription = request.CargoDescription,
            ParcelType = request.ParcelType,
            WeightKg = request.EstimatedWeightTon * 1000,
            ParcelCount = request.ParcelCount,
            ParcelSize = request.ParcelSize,
            DeliveryDate = request.DeliveryDate,
            DeliveryTimeWindow = deliveryTimeWindow,
            PaymentMethod = request.PaymentMethod,
            OfferPrice = offerPrice,
            CreatedAt = request.CreatedAt,
            StartedAt = shipment.StartedAt,
            CompletedAt = shipment.CompletedAt
        };
    }

    /// <summary>
    /// Sends an FCM notification to the merchant when shipment status changes.
    /// </summary>
    private async Task NotifyMerchantStatusChangeAsync(Guid shipmentId, string title, string body, CancellationToken cancellationToken)
    {
        try
        {
            var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
            if (shipment == null) return;
            var request = await _db.ShipmentRequests.FindAsync([shipment.ShipmentRequestId], cancellationToken);
            if (request == null) return;
            var merchantProfile = await _db.MerchantProfiles.FindAsync([request.MerchantProfileId], cancellationToken);
            if (merchantProfile == null) return;
            await _notificationService.SendNotificationAsync(
                merchantProfile.UserId,
                title,
                body,
                null,
                true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify merchant of status change for shipment {ShipmentId} ({Title})", shipmentId, title);
        }
    }

    private async Task NotifyMerchantShipmentDeliveredAsync(Guid shipmentId, CancellationToken cancellationToken)
    {
        try
        {
            var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
            if (shipment == null) return;
            var request = await _db.ShipmentRequests.FindAsync([shipment.ShipmentRequestId], cancellationToken);
            if (request == null) return;
            var merchantProfile = await _db.MerchantProfiles.FindAsync([request.MerchantProfileId], cancellationToken);
            if (merchantProfile == null) return;
            var title = "تم التسليم";
            var body = $"تم تسليم طلب رقم {request.RequestNumber} بنجاح!";
            await _notificationService.SendNotificationAsync(
                merchantProfile.UserId,
                title,
                body,
                null,
                true,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify merchant of delivery for shipment {ShipmentId}", shipmentId);
        }
    }
}
