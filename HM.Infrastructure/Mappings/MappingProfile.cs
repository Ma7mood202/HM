using AutoMapper;
using HM.Application.Common.DTOs.Driver;
using HM.Application.Common.DTOs.Merchant;
using HM.Application.Common.DTOs.Shipment;
using HM.Domain.Entities;

namespace HM.Infrastructure.Mappings;

/// <summary>
/// AutoMapper profile: Domain entities ↔ Application DTOs.
/// No business logic; flatten nested data and map enums. Price hidden from driver DTOs.
/// </summary>
public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // CreateShipmentRequestDto -> ShipmentRequest (partial; Id, MerchantProfileId, Status, CreatedAt set in service)
        CreateMap<CreateShipmentRequestDto, ShipmentRequest>()
            .ForMember(d => d.Id, o => o.Ignore())
            .ForMember(d => d.MerchantProfileId, o => o.Ignore())
            .ForMember(d => d.Status, o => o.Ignore())
            .ForMember(d => d.CreatedAt, o => o.Ignore());

        // ShipmentRequest -> ShipmentRequestDto (OffersCount set in service)
        CreateMap<ShipmentRequest, ShipmentRequestDto>()
            .ForMember(d => d.OffersCount, o => o.Ignore());

        // ShipmentOffer -> ShipmentOfferDto (TruckAccountName, PickupLocation, DropoffLocation set in service)
        CreateMap<ShipmentOffer, ShipmentOfferDto>()
            .ForMember(d => d.TruckAccountName, o => o.Ignore())
            .ForMember(d => d.PickupLocation, o => o.Ignore())
            .ForMember(d => d.DropoffLocation, o => o.Ignore());

        // Shipment -> ShipmentStatusDto (UpdatedAt set in service)
        CreateMap<Shipment, ShipmentStatusDto>()
            .ForMember(d => d.ShipmentId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.UpdatedAt, o => o.Ignore());

        // DriverProfile -> DriverProfileDto (HasNationalId from NationalIdImageUrl)
        CreateMap<DriverProfile, DriverProfileDto>()
            .ForMember(d => d.HasNationalId, o => o.MapFrom(dp => !string.IsNullOrEmpty(dp.NationalIdImageUrl)));

        // Shipment -> AssignedShipmentDto (driver view: no price; other fields set in service from request/truck)
        CreateMap<Shipment, AssignedShipmentDto>()
            .ForMember(d => d.ShipmentId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.PickupLocation, o => o.Ignore())
            .ForMember(d => d.DropoffLocation, o => o.Ignore())
            .ForMember(d => d.CargoDescription, o => o.Ignore())
            .ForMember(d => d.EstimatedWeight, o => o.Ignore())
            .ForMember(d => d.Notes, o => o.Ignore())
            .ForMember(d => d.TruckPlateNumber, o => o.Ignore());
    }
}
