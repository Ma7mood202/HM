using AutoMapper;
using HM.Application.Common.DTOs.Driver;
using HM.Application.Common.DTOs.Shipment;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
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

    public DriverService(IApplicationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<DriverProfileDto> UploadNationalIdAsync(Guid driverProfileId, UploadNationalIdRequest request, CancellationToken cancellationToken = default)
    {
        var profile = await _db.DriverProfiles.FindAsync([driverProfileId], cancellationToken);
        if (profile == null)
            throw new InvalidOperationException("Driver profile not found.");

        profile.NationalIdImageUrl = request.NationalIdImage;
        await _db.SaveChangesAsync(cancellationToken);

        return _mapper.Map<DriverProfileDto>(profile);
    }

    public async Task<ShipmentStatusDto> StartShipmentAsync(Guid driverProfileId, Guid shipmentId, CancellationToken cancellationToken = default)
    {
        var shipment = await _db.Shipments.FindAsync([shipmentId], cancellationToken);
        if (shipment == null || shipment.DriverProfileId != driverProfileId)
            throw new InvalidOperationException("Shipment not found or not assigned to this driver.");
        if (shipment.Status != ShipmentStatus.Ready)
            throw new InvalidOperationException("Shipment is not ready to start.");

        shipment.Status = ShipmentStatus.InTransit;
        shipment.StartedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<ShipmentStatusDto>(shipment);
        dto.UpdatedAt = DateTime.Now;
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
        dto.UpdatedAt = DateTime.Now;
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
        shipment.CompletedAt = DateTime.Now;
        await _db.SaveChangesAsync(cancellationToken);

        var dto = _mapper.Map<ShipmentStatusDto>(shipment);
        dto.UpdatedAt = DateTime.Now;
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
}
