using AutoMapper;
using HM.Application.Common.DTOs.Driver;
using HM.Application.Common.DTOs.Shipment;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Entities;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HM.Infrastructure.Services;

/// <summary>
/// Driver operations: upload national ID, start/pause/complete shipment, get assigned shipment (no price).
/// </summary>
public sealed class DriverService : IDriverService
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;

    public DriverService(IApplicationDbContext db, IMapper mapper, INotificationService notificationService)
    {
        _db = db;
        _mapper = mapper;
        _notificationService = notificationService;
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
        dto.EstimatedWeight = request.EstimatedWeight;
        dto.Notes = request.Notes;
        dto.TruckPlateNumber = truck?.PlateNumber;
        return dto;
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

        return await BuildDriverShipmentDetailsAsync(shipment, request, cancellationToken);
    }

    public async Task<DriverShipmentDetailsResponse> MarkArrivedAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var (shipment, request) = await LoadShipmentOwnedByDriverAsync(driverUserId, shipmentId, cancellationToken);
        if (shipment.Status != ShipmentStatus.InTransit)
            throw new InvalidOperationException("Shipment must be in transit before marking as arrived.");

        shipment.Status = ShipmentStatus.Arrived;
        await _db.SaveChangesAsync(cancellationToken);

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
            ? $"{(from.HasValue ? from.Value.ToString("hh\\:mm") : "?")} - {(to.HasValue ? to.Value.ToString("hh\\:mm") : "?")}"
            : "";

        return new DriverShipmentDetailsResponse
        {
            ShipmentId = shipment.Id,
            RequestNumber = request.RequestNumber,
            Status = shipment.Status,
            PickupAddressText = request.PickupLocation,
            PickupLat = request.PickupLat,
            PickupLng = request.PickupLng,
            DropoffAddressText = request.DropoffLocation,
            DropoffLat = request.DropoffLat,
            DropoffLng = request.DropoffLng,
            MerchantName = merchantName,
            MerchantPhone = merchantPhone,
            RecipientName = request.SenderName,
            RecipientPhone = request.SenderPhone,
            ParcelDescription = request.CargoDescription,
            ParcelType = request.ParcelType,
            WeightKg = request.EstimatedWeight,
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
        catch
        {
            // Don't fail the main flow
        }
    }
}
