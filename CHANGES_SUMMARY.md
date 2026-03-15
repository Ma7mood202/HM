# Backend Changes Summary

Summary of changes made for Merchant Role, Truck Account Role, Location APIs, and related DTOs/entities.

---

## 1. Location APIs (Governorates & Regions)

| What | Affected |
|------|----------|
| **New entities** | `HM.Domain/Entities/Governorate.cs`, `HM.Domain/Entities/Region.cs` (Region has `GovernorateId`) |
| **New DTOs** | `HM.Application/Common/DTOs/Location/GovernorateDto.cs`, `RegionDto.cs` |
| **New controller** | `Hm.WebApi/Controllers/LocationController.cs` |
| **DbContext** | `IApplicationDbContext` + `ApplicationDbContext`: added `DbSet<Governorate>`, `DbSet<Region>` |
| **EF config** | `HM.Infrastructure/Configurations/GovernorateConfiguration.cs`, `RegionConfiguration.cs` |

**Endpoints:**

- **GET /api/governorates** – List all governorates (for pickup/drop-off).
- **GET /api/regions** – List regions; optional query `governorateId` to filter by governorate.

---

## 2. Create Shipment Request Flow (Merchant)

| What | Affected |
|------|----------|
| **Request DTO** | `CreateShipmentRequestRequest.cs`: `ParcelWeightTon` (replaces `ParcelWeightKg`), `IsFragile`, `ReceiverPhoneNumber`, `PickupGovernorateId`, `PickupRegionId`, `DropoffGovernorateId`, `DropoffRegionId`, `TruckBodyType`. Removed: `PickupLat`, `PickupLng`, `DropoffLat`, `DropoffLng`. |
| **Entity** | `ShipmentRequest.cs`: same fields as above; `EstimatedWeightTon`; governorate/region FKs and navigations; lat/lon removed. |
| **Details response** | `ShipmentRequestDetailsResponse.cs`: `ParcelWeightTon`, `EstimatedTime`, `DistanceKm`, `PickupRegion`, `DropoffRegion`, `DriverImage`; lat/lon removed. |
| **Controller** | `MerchantController.cs`: **POST /api/merchant/shipment/shipment-requests** (new route); existing **POST /api/merchant/shipment-requests** unchanged. |
| **Service** | `MerchantService.cs`: create uses new request fields; validation for `ParcelWeightTon`; `EstimateTimeAndDistance()` for `EstimatedTime` / `DistanceKm`; details build uses regions and driver image. |

**Response:** Create and details now include `estimatedTime`, `distanceKm` (derived from governorate/region).

---

## 3. Shipment Requests List (Merchant)

| What | Affected |
|------|----------|
| **List response** | `ShipmentRequestSummaryResponse.cs`: `PickupGovernorate`, `PickupRegion`, `AmountDue`. |
| **Service** | `MerchantService.GetMyShipmentRequestsAsync`: loads governorates/regions and accepted offer price; maps to `PickupGovernorate`, `PickupRegion`, `AmountDue`. |

**Endpoint:** **GET /api/merchant/shipment-requests** – response now includes `pickupGovernorate`, `pickupRegion`, `pickupAreaOrText`, `amountDue`.

---

## 4. Shipment Request Offers (Merchant)

| What | Affected |
|------|----------|
| **Offer response** | `ShipmentOfferResponse.cs`: `TruckAccountImage`, `TruckSize`, `TruckType`, `ParcelWeightTon`. |
| **Service** | `MerchantService.GetOffersForMyRequestAsync`: loads first truck per account; maps truck type/size, account avatar, request `EstimatedWeightTon`. |
| **Reject offer** | `IMerchantService` + `MerchantService`: `RejectOfferAsync`. `MerchantController`: **POST /api/merchant/shipment-requests/{id}/offers/{offerId}/reject** (204 No Content). |

**Endpoints:**

- **GET /api/merchant/shipment-requests/{id}/offers** – response includes `truckAccountImage`, `truckSize`, `truckType`, `parcelWeight` (tons).
- **POST .../offers/{offerId}/reject** – merchant can reject an offer.

---

## 5. Shipment Details (Merchant)

| What | Affected |
|------|----------|
| **Details response** | `ShipmentRequestDetailsResponse.cs`: `PickupRegion`, `DropoffRegion`, `DriverImage` (and existing details). |
| **Service** | `MerchantService.BuildDetailsResponseAsync`: loads region names by ID; sets `DriverImage` from assigned driver profile. |

**Endpoint:** **GET /api/merchant/shipment-requests/{id}** – response includes `pickupRegion`, `dropoffRegion`, `driverImage`.

---

## 6. Truck – Open Shipments & Filters

| What | Affected |
|------|----------|
| **Filter DTO** | `ShipmentRequestFilterDto.cs`: `TruckBodyType`, `ParcelWeightTon`, `PickupRegionId`, `DropoffRegionId`, `Latitude`, `Longitude`, `RadiusKm`. |
| **Entity** | `ShipmentRequest`: `RequiredTruckBodyType`; `Truck`: optional `BodyType` (Open/Closed/Refrigerated). |
| **List DTO** | `ShipmentListItemDto.cs`: `ReferenceNumber`, `PickupRegion`, `DropoffRegion`, `EstimatedWeightTon`, `ParcelType`. |
| **Service** | `TruckService.GetOpenShipmentRequestsAsync`: filters by `TruckBodyType`, `ParcelWeightTon`, `PickupRegionId`, `DropoffRegionId`; uses `EstimatedWeightTon`; loads region names for list. |

**Endpoint:** **GET /api/truck/shipments/open** – filters: `truckType` (body: Open/Closed/Refrigerated), `parcelWeight` (ton), `pickupRegion`, `dropoffRegion`. “Near me” can be done by sending driver’s `pickupRegionId`.

---

## 7. Truck – Shipment Request Details

| What | Affected |
|------|----------|
| **Details DTO** | `ShipmentDetailsDto.cs`: `ReferenceNumber`, `PickupRegion`, `DropoffRegion`, `DriverImage`, `TruckType`. |
| **Service** | `TruckService.GetShipmentRequestDetailsAsync`: loads region names; when a shipment exists, loads driver name/image, truck plate/type. |

**Endpoint:** **GET /api/truck/requests/{id}** – response includes `referenceNumber`, `driverName`, `driverImage`, `truckPlateNumber`, `truckType`, `pickupRegion`, `dropoffRegion`.

---

## 8. Submit Offer (Truck)

| What | Affected |
|------|----------|
| **Request** | `SubmitOfferRequest.cs`: `ExpirationAt` removed. |
| **Entity** | `ShipmentOffer.cs`: `ExpirationAt` made nullable. |
| **Service** | `TruckService.SubmitOfferAsync`: sets `ExpirationAt = null` when creating offer. |

**Endpoint:** **POST /api/truck/offers** – no `expirationAt` in request or creation.

---

## 9. Driver Assignment (Truck)

| What | Affected |
|------|----------|
| **New DTO** | `AssignDriverRequest.cs`: `DriverProfileId`. |
| **Service** | `ITruckService` + `TruckService`: `AssignDriverAsync(truckAccountId, shipmentId, driverProfileId)`. |
| **Controller** | `TruckController`: **POST /api/truck/shipments/{id}/assign-driver** with body `{ "driverProfileId": "..." }`. |

**Endpoint:** **POST /api/truck/shipments/{id}/assign-driver** – assign a specific driver to the shipment (in addition to existing assign-self and invite-driver).

---

## 10. My Offers (Truck)

| What | Affected |
|------|----------|
| **Offer DTO** | `ShipmentOfferDto.cs`: `ReferenceNumber`, `PickupRegion`, `DropoffRegion`, `ParcelType`, `TruckAccountImage`; `ExpirationAt` nullable. |
| **Service** | `TruckService.GetMyOffersAsync`: loads requests and regions; maps request number, region names, parcel type, truck account avatar. |

**Endpoint:** **GET /api/truck/offers** – response includes `referenceNumber`, `pickupRegion`, `dropoffRegion`, `parcelType`, `truckAccountImage`.

---

## 11. Notifications Payload

| What | Affected |
|------|----------|
| **Service** | `MerchantService.NotifyDriverOfferAcceptedAsync`: notification `data` JSON now includes `shipmentRequestId` and `offerId`. |

**Payload:** Notifications (e.g. “offer accepted”) include `shipmentRequestId` and `offerId` when applicable.

---

## 12. Truck Create & Enums

| What | Affected |
|------|----------|
| **Enum** | `HM.Domain/Enums/TruckBodyType.cs`: Open, Closed, Refrigerated. |
| **Entity** | `Truck.cs`: optional `BodyType` (TruckBodyType). |
| **Request** | `CreateTruckRequest.cs`: optional `BodyType`. |
| **Service** | `TruckService.CreateTruckAsync`: sets `BodyType` from request. |

**Endpoint:** **POST /api/truck/trucks** – body can include `bodyType` (0=Open, 1=Closed, 2=Refrigerated).

---

## 13. Driver & Other Services (weight/response)

| What | Affected |
|------|----------|
| **DriverService** | Uses `request.EstimatedWeightTon`; builds driver shipment details with `WeightKg = request.EstimatedWeightTon * 1000`; pickup/dropoff lat/lng in response set to `null` (no longer on entity). |
| **TruckService** | All references to request/shipment weight use `EstimatedWeightTon`; list/details DTOs use `EstimatedWeightTon` and/or `EstimatedWeight` where kept for compatibility. |
| **MappingProfile** | `ShipmentRequest` ↔ DTOs: `EstimatedWeight` mapped from `EstimatedWeightTon`; `CreateShipmentRequestDto` → `ShipmentRequest`: `EstimatedWeightTon` from `EstimatedWeight`. |

---

## 14. EF Configuration & Snapshot

| What | Affected |
|------|----------|
| **Configurations** | `ShipmentRequestConfiguration`: governorate/region FKs, `EstimatedWeightTon`, `IsFragile`, `ReceiverPhoneNumber`, `RequiredTruckBodyType`; lat/lon removed. `TruckConfiguration`: `BodyType`. `ShipmentOfferConfiguration`: `ExpirationAt` optional. |
| **Migrations** | The migration that added Governorates/Regions and the above schema changes was **removed** (you add migrations yourself). |
| **Snapshot** | `ApplicationDbContextModelSnapshot.cs` was **reverted** to the previous model (no Governorates/Regions, `EstimatedWeight` + lat/lon on `ShipmentRequest`, no `BodyType` on Truck, required `ExpirationAt` on ShipmentOffer) so that a new migration can be generated when you run `dotnet ef migrations add`. |

---

## 15. Postman Collection

| What | Affected |
|------|----------|
| **File** | `HM.postman_collection (1).json` |
| **Variables** | Added `driverProfileId`. |
| **Location** | New folder: **Get Governorates**, **Get Regions** (with optional `governorateId`). |
| **Merchant** | Create Shipment Request body updated (tons, governorate/region IDs, isFragile, receiverPhoneNumber; no lat/lon). New request: **Create Shipment Request (shipment)** for POST `/api/merchant/shipment/shipment-requests`. New: **Reject Offer**. |
| **Truck** | Create Truck: body includes optional `bodyType`. Get Open Shipment Requests: query params for new filters (e.g. `filter.truckBodyType`, `filter.parcelWeightTon`, `filter.pickupRegionId`, `filter.dropoffRegionId`). Submit Offer: body without `expirationAt`. New: **Assign Driver**. Descriptions updated for new response fields (referenceNumber, pickupRegion, dropoffRegion, parcelType, truckAccountImage, etc.). |

---

## File List (New / Modified)

**New files**

- `HM.Domain/Entities/Governorate.cs`
- `HM.Domain/Entities/Region.cs`
- `HM.Domain/Enums/TruckBodyType.cs`
- `HM.Application/Common/DTOs/Location/GovernorateDto.cs`
- `HM.Application/Common/DTOs/Location/RegionDto.cs`
- `HM.Application/Common/DTOs/Truck/AssignDriverRequest.cs`
- `Hm.WebApi/Controllers/LocationController.cs`
- `HM.Infrastructure/Configurations/GovernorateConfiguration.cs`
- `HM.Infrastructure/Configurations/RegionConfiguration.cs`

**Modified files**

- `HM.Domain/Entities/ShipmentRequest.cs`
- `HM.Domain/Entities/ShipmentOffer.cs`
- `HM.Domain/Entities/Truck.cs`
- `HM.Application/Common/DTOs/Merchant/CreateShipmentRequestRequest.cs`
- `HM.Application/Common/DTOs/Merchant/ShipmentRequestDetailsResponse.cs`
- `HM.Application/Common/DTOs/Merchant/ShipmentRequestSummaryResponse.cs`
- `HM.Application/Common/DTOs/Merchant/ShipmentOfferResponse.cs`
- `HM.Application/Common/DTOs/Merchant/ShipmentOfferDto.cs`
- `HM.Application/Common/DTOs/Truck/SubmitOfferRequest.cs`
- `HM.Application/Common/DTOs/Truck/ShipmentRequestFilterDto.cs`
- `HM.Application/Common/DTOs/Truck/ShipmentListItemDto.cs`
- `HM.Application/Common/DTOs/Truck/CreateTruckRequest.cs`
- `HM.Application/Common/DTOs/Shipment/ShipmentDetailsDto.cs`
- `HM.Application/Interfaces/Persistence/IApplicationDbContext.cs`
- `HM.Application/Interfaces/Services/IMerchantService.cs`
- `HM.Application/Interfaces/Services/ITruckService.cs`
- `HM.Infrastructure/Data/ApplicationDbContext.cs`
- `HM.Infrastructure/Configurations/ShipmentRequestConfiguration.cs`
- `HM.Infrastructure/Configurations/TruckConfiguration.cs`
- `HM.Infrastructure/Configurations/ShipmentOfferConfiguration.cs`
- `HM.Infrastructure/Services/MerchantService.cs`
- `HM.Infrastructure/Services/TruckService.cs`
- `HM.Infrastructure/Services/DriverService.cs`
- `HM.Infrastructure/Mappings/MappingProfile.cs`
- `Hm.WebApi/Controllers/MerchantController.cs`
- `Hm.WebApi/Controllers/TruckController.cs`
- `HM.Infrastructure/Migrations/ApplicationDbContextModelSnapshot.cs` (reverted)
- `HM.postman_collection (1).json`

**Deleted (migration)**

- `HM.Infrastructure/Migrations/20260304212936_AddGovernoratesRegionsAndShipmentRequestUpdates.cs`
- `HM.Infrastructure/Migrations/20260304212936_AddGovernoratesRegionsAndShipmentRequestUpdates.Designer.cs`

---

## Next Steps

1. **Add migration**  
   From repo root:
   ```bash
   dotnet ef migrations add AddGovernoratesRegionsAndShipmentRequestUpdates --project HM.Infrastructure --startup-project Hm.WebApi
   ```
   Then apply it when ready (e.g. `dotnet ef database update`).

2. **Seed Governorates & Regions**  
   Populate `Governorates` and `Regions` (e.g. in a seed migration or startup) so create-shipment and filters work with real data.

3. **Postman**  
   Use the updated collection; set `baseUrl`, `token`, `shipmentRequestId`, `offerId`, `shipmentId`, `truckId`, `driverProfileId` as needed. For create shipment and filters, use IDs returned from **Get Governorates** and **Get Regions**.
