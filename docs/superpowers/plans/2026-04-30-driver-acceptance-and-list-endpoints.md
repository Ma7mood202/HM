# Driver Acceptance Flow + Merchant/Truck List Endpoints + Driver Realtime Tracking

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Address four mobile-frontend requests in one shippable batch: (1) merchant endpoint to list their shipments for tracking, (2) truck account endpoint to list drivers for assignment, (3) explicit driver-acceptance flow with 15-minute timeout for direct assigns (QR/deep-link stays implicit), (4) authenticated SignalR group joins so drivers can subscribe to live tracking for their assigned shipments.

**Architecture:** Insert a new `PendingDriverAcceptance` status between `AwaitingDriver` and `Ready` for the direct-assign path only. The existing QR/deep-link flow (`AcceptDriverInvitationAsync`) bumps the shipment straight to `Ready` because the driver already explicitly engaged. A `BackgroundService` polls every 60 seconds to auto-revert assignments stuck in `PendingDriverAcceptance` past 15 minutes. Drivers subscribe to the same `ShipmentTrackingHub` group as merchants — we add a server-side authorization check inside `JoinShipmentTracking` so a user can only join groups they are a participant in (merchant of the request, assigned driver, or truck account that owns the accepted offer).

**Tech Stack:** .NET 8 SDK 8.0.413, ASP.NET Core, EF Core (Npgsql), AutoMapper, SignalR, Identity. PostgreSQL. No xUnit project exists in this repo — verification is via `dotnet build` and a manual end-to-end API smoke test (curl). Adding a test project is out of scope for this plan.

---

## Scope Check

This plan covers four related but independent slices. They share types (the new enum value, the `AssignedAt` field, the new accept/reject endpoints), so splitting into four separate plans would introduce coordination overhead and a stale `develop` branch in between. Keeping them together is correct.

---

## File Structure

| Path | Created/Modified | Responsibility |
|---|---|---|
| `HM.Domain/Enums/ShipmentStatus.cs` | Modify | Add `PendingDriverAcceptance` value between `AwaitingDriver` and `Ready` |
| `HM.Domain/Entities/Shipment.cs` | Modify | Add `AssignedAt` (nullable DateTime) for timeout tracking |
| `HM.Infrastructure/Configurations/ShipmentConfiguration.cs` | Modify | Map the new `AssignedAt` column |
| `HM.Infrastructure/Migrations/<timestamp>_AddDriverAcceptanceFlow.cs` | Create | EF migration: add `AssignedAt` column |
| `HM.Application/Common/DTOs/Driver/DriverSummaryDto.cs` | Create | Lightweight driver projection for the truck-side list |
| `HM.Application/Common/DTOs/Merchant/MerchantShipmentSummaryDto.cs` | Create | Lightweight shipment projection for merchant tracking list |
| `HM.Application/Interfaces/Services/ITruckService.cs` | Modify | Add `GetAvailableDriversAsync` + change the contract docs for `AssignDriverAsync` (now produces `PendingDriverAcceptance`) |
| `HM.Application/Interfaces/Services/IMerchantService.cs` | Modify | Add `GetMyShipmentsAsync` |
| `HM.Application/Interfaces/Services/IDriverService.cs` | Modify | Add `AcceptAssignmentAsync` and `RejectAssignmentAsync` |
| `HM.Infrastructure/Services/TruckService.cs` | Modify | Implement `GetAvailableDriversAsync`; rewire `AssignDriverAsync` to set `PendingDriverAcceptance`, stamp `AssignedAt`, notify driver |
| `HM.Infrastructure/Services/MerchantService.cs` | Modify | Implement `GetMyShipmentsAsync` |
| `HM.Infrastructure/Services/DriverService.cs` | Modify | Implement `AcceptAssignmentAsync` (→`Ready`) and `RejectAssignmentAsync` (→`AwaitingDriver`, clear DriverProfileId), notify truck account |
| `HM.Infrastructure/Services/AuthService.cs` | Modify | In `AcceptDriverInvitationAsync`, set shipment `Status = Ready` (implicit accept on QR/deep-link) |
| `HM.Infrastructure/Services/PendingDriverAssignmentTimeoutService.cs` | Create | `BackgroundService` polling every 60s for assignments older than 15 min; revert to `AwaitingDriver`, notify merchant |
| `HM.Infrastructure/DependencyInjection.cs` | Modify | Register the timeout background service as `IHostedService` |
| `Hm.WebApi/Controllers/TruckController.cs` | Modify | Add `GET /api/truck/drivers` |
| `Hm.WebApi/Controllers/MerchantController.cs` | Modify | Add `GET /api/merchant/shipments` |
| `Hm.WebApi/Controllers/DriverShipmentsController.cs` | Modify | Add `POST /api/driver/shipments/{id}/accept` and `/reject` |
| `Hm.WebApi/Hubs/ShipmentTrackingHub.cs` | Modify | Authorize `JoinShipmentTracking` against the user's relationship to the shipment |
| `HM.postman_collection.json` | Modify | Add the four new endpoints under their respective folders |

Each task below is self-contained: it references a single feature slice, includes the full code diff, and ends with a verification step.

---

## Note on Verification

There is **no test project** in this solution. Each task ends with one or both of:
- `dotnet build HM.sln` — must complete with `Build succeeded` and zero errors.
- A `curl` smoke command against a locally-running instance (`dotnet run --project Hm.WebApi`), with the expected JSON shape inline.

Run the API locally with `dotnet run --project Hm.WebApi --launch-profile https` and a Postgres instance reachable per `appsettings.Development.json`. Auth tokens come from `/api/auth/login`. If you don't have seed data with all three user types, skip the curl step and rely on `dotnet build` for that task — the final integration task (Task 11) is the end-to-end smoke check.

---

## Task 1: Add `PendingDriverAcceptance` to ShipmentStatus enum

**Files:**
- Modify: `HM.Domain/Enums/ShipmentStatus.cs`

- [ ] **Step 1: Add the new enum value**

Replace the file contents with:

```csharp
namespace HM.Domain.Enums;

public enum ShipmentStatus
{
    AwaitingDriver,
    PendingDriverAcceptance, // Driver assigned by truck account; awaiting driver's explicit accept (15 min timeout)
    Ready,
    InTransit,
    Arrived,    // Driver marked "تم الوصول" (after Start, before Received)
    Paused,
    Completed,
    Cancelled
}
```

> **Important:** Inserting the value in the middle of the enum changes the integer ordinals of subsequent values. Because `ShipmentConfiguration.cs` already sets `.HasConversion<string>()`, the database column stores the **enum name**, not the int — so existing rows are not broken by reordering. Verify this in Step 2 before continuing.

- [ ] **Step 2: Verify enum is stored as string**

Read `HM.Infrastructure/Configurations/ShipmentConfiguration.cs` lines 25–28. Confirm:

```csharp
builder.Property(s => s.Status)
    .HasConversion<string>()
    .HasMaxLength(32)
    .IsRequired();
```

If `.HasConversion<string>()` is missing, STOP and add it before continuing. If it's present, proceed.

- [ ] **Step 3: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`. Warnings about non-exhaustive `switch` expressions on `ShipmentStatus` may appear — record any file paths that warn; we'll revisit them in later tasks.

- [ ] **Step 4: Commit**

```bash
git add HM.Domain/Enums/ShipmentStatus.cs
git commit -m "feat(domain): add PendingDriverAcceptance shipment status"
```

---

## Task 2: Add `AssignedAt` field to Shipment + EF migration

**Files:**
- Modify: `HM.Domain/Entities/Shipment.cs`
- Modify: `HM.Infrastructure/Configurations/ShipmentConfiguration.cs`
- Create: `HM.Infrastructure/Migrations/<timestamp>_AddDriverAcceptanceFlow.cs` (generated)

- [ ] **Step 1: Add `AssignedAt` to the entity**

Edit `HM.Domain/Entities/Shipment.cs`. Add the new property right after `CompletedAt`:

```csharp
using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class Shipment
{
    public Guid Id { get; set; }
    public Guid ShipmentRequestId { get; set; }
    public Guid AcceptedOfferId { get; set; }
    public Guid TruckId { get; set; }
    public Guid? DriverProfileId { get; set; }
    public ShipmentStatus Status { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    /// <summary>UTC timestamp when a driver was last assigned and is awaiting acceptance. Used by the timeout job.</summary>
    public DateTime? AssignedAt { get; set; }
    public double? CurrentLat { get; set; }
    public double? CurrentLng { get; set; }
    public DateTime? LocationUpdatedAt { get; set; }
}
```

- [ ] **Step 2: Map `AssignedAt` in EF configuration**

Edit `HM.Infrastructure/Configurations/ShipmentConfiguration.cs`. Add `builder.Property(s => s.AssignedAt);` next to the other nullable property mappings:

```csharp
        builder.Property(s => s.StartedAt);
        builder.Property(s => s.CompletedAt);
        builder.Property(s => s.AssignedAt);
        builder.Property(s => s.CurrentLat);
        builder.Property(s => s.CurrentLng);
        builder.Property(s => s.LocationUpdatedAt);
```

- [ ] **Step 3: Generate the EF migration**

Run from the repo root:

```bash
dotnet ef migrations add AddDriverAcceptanceFlow --project HM.Infrastructure --startup-project Hm.WebApi
```

Expected: a new file `HM.Infrastructure/Migrations/<timestamp>_AddDriverAcceptanceFlow.cs` is created with `AddColumn<DateTime>("AssignedAt", "Shipments", nullable: true)` in `Up` and a corresponding `DropColumn` in `Down`. No other table changes should appear — if the diff includes anything besides `AssignedAt`, abort the migration with `dotnet ef migrations remove --project HM.Infrastructure --startup-project Hm.WebApi` and investigate.

- [ ] **Step 4: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 5: Commit**

```bash
git add HM.Domain/Entities/Shipment.cs HM.Infrastructure/Configurations/ShipmentConfiguration.cs HM.Infrastructure/Migrations/
git commit -m "feat(db): add Shipment.AssignedAt for driver-acceptance timeout"
```

---

## Task 3: Modify TruckService.AssignDriverAsync to enter PendingDriverAcceptance + notify driver

**Files:**
- Modify: `HM.Infrastructure/Services/TruckService.cs`

- [ ] **Step 1: Inject INotificationService into TruckService**

Edit `HM.Infrastructure/Services/TruckService.cs`. Update the constructor and field:

```csharp
public sealed class TruckService : ITruckService
{
    private readonly IApplicationDbContext _db;
    private readonly IMapper _mapper;
    private readonly INotificationService _notificationService;

    public TruckService(IApplicationDbContext db, IMapper mapper, INotificationService notificationService)
    {
        _db = db;
        _mapper = mapper;
        _notificationService = notificationService;
    }
```

(`INotificationService` is already in `HM.Application.Interfaces.Services` — make sure the using is present at the top of the file; it should be, since we already use it elsewhere. If not, add `using HM.Application.Interfaces.Services;`.)

- [ ] **Step 2: Replace `AssignDriverAsync` body to use `PendingDriverAcceptance`, stamp `AssignedAt`, and notify the driver**

In `TruckService.cs`, replace the entire existing `AssignDriverAsync` method (lines ~373–394) with:

```csharp
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
            catch
            {
                // Non-fatal
            }
        }

        return await BuildShipmentDetailsDtoAsync(shipment.Id, cancellationToken);
    }
```

- [ ] **Step 3: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add HM.Infrastructure/Services/TruckService.cs
git commit -m "feat(truck): assign-driver now enters PendingDriverAcceptance and notifies driver"
```

---

## Task 4: Driver accept/reject — interface + DTO

**Files:**
- Modify: `HM.Application/Interfaces/Services/IDriverService.cs`

- [ ] **Step 1: Add the two new methods to the IDriverService contract**

Open `HM.Application/Interfaces/Services/IDriverService.cs`. Add these two methods (place them after `GetAssignedShipmentAsync`, before `GetMyShipmentDetailsAsync`):

```csharp
    /// <summary>
    /// Driver accepts an assignment in PendingDriverAcceptance state. Transitions to Ready.
    /// </summary>
    Task<DriverShipmentDetailsResponse> AcceptAssignmentAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Driver rejects an assignment in PendingDriverAcceptance state. Reverts to AwaitingDriver and clears DriverProfileId.
    /// </summary>
    Task<DriverShipmentDetailsResponse> RejectAssignmentAsync(Guid driverUserId, Guid shipmentId, CancellationToken cancellationToken = default);
```

- [ ] **Step 2: Build**

Run: `dotnet build HM.sln`
Expected: build fails with errors saying `DriverService` does not implement `AcceptAssignmentAsync` and `RejectAssignmentAsync`. This is correct — Task 5 implements them.

- [ ] **Step 3: Commit**

```bash
git add HM.Application/Interfaces/Services/IDriverService.cs
git commit -m "feat(driver): add AcceptAssignmentAsync/RejectAssignmentAsync contracts"
```

---

## Task 5: Implement DriverService.AcceptAssignmentAsync + RejectAssignmentAsync

**Files:**
- Modify: `HM.Infrastructure/Services/DriverService.cs`

- [ ] **Step 1: Add the two methods to DriverService**

Open `HM.Infrastructure/Services/DriverService.cs`. Insert these two methods right before `GetMyShipmentDetailsAsync`:

```csharp
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
            var data = System.Text.Json.JsonSerializer.Serialize(new { shipmentId = shipment.Id });
            await _notificationService.SendNotificationAsync(
                truckAccount.UserId,
                title,
                body,
                data,
                true,
                cancellationToken);
        }
        catch
        {
            // Non-fatal
        }
    }
```

> Note: `LoadShipmentOwnedByDriverAsync` (lines 281–296) checks `shipment.DriverProfileId != driverProfileId` and throws Unauthorized if not. That's exactly the gate we want for accept/reject — the driver can only accept/reject what's currently assigned to them.

- [ ] **Step 2: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add HM.Infrastructure/Services/DriverService.cs
git commit -m "feat(driver): implement accept/reject assignment with truck-account notification"
```

---

## Task 6: Add accept/reject HTTP endpoints

**Files:**
- Modify: `Hm.WebApi/Controllers/DriverShipmentsController.cs`

- [ ] **Step 1: Add the two endpoints**

Open `Hm.WebApi/Controllers/DriverShipmentsController.cs`. Insert these two methods right after `GetShipmentDetails` (currently at line ~40):

```csharp
    /// <summary>Driver accepts an assignment in PendingDriverAcceptance. Transitions to Ready.</summary>
    [HttpPost("{shipmentId:guid}/accept")]
    public async Task<IActionResult> AcceptAssignment(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.AcceptAssignmentAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }

    /// <summary>Driver rejects an assignment in PendingDriverAcceptance. Reverts to AwaitingDriver.</summary>
    [HttpPost("{shipmentId:guid}/reject")]
    public async Task<IActionResult> RejectAssignment(Guid shipmentId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var result = await _driverService.RejectAssignmentAsync(userId, shipmentId, cancellationToken);

        await _hubContext.Clients
            .Group($"shipment-{shipmentId}")
            .SendAsync("StatusChanged", new ShipmentStatusChangeEvent
            {
                ShipmentId = shipmentId,
                Status = result.Status,
                ChangedAt = DateTime.UtcNow
            }, cancellationToken);

        return Ok(result);
    }
```

- [ ] **Step 2: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add Hm.WebApi/Controllers/DriverShipmentsController.cs
git commit -m "feat(api): add POST /api/driver/shipments/{id}/{accept,reject}"
```

---

## Task 7: Auto-accept when driver claims via QR/deep-link invitation

**Files:**
- Modify: `HM.Infrastructure/Services/AuthService.cs`

- [ ] **Step 1: Set shipment Status = Ready when invitation is accepted**

Open `HM.Infrastructure/Services/AuthService.cs`. Find `AcceptDriverInvitationAsync` (around line 303). Locate the block that updates the shipment (lines ~362–366):

```csharp
        var shipment = await _db.Shipments.FindAsync([invitation.ShipmentId], cancellationToken);
        if (shipment != null)
        {
            shipment.DriverProfileId = driverProfile.Id;
        }
```

Replace it with:

```csharp
        var shipment = await _db.Shipments.FindAsync([invitation.ShipmentId], cancellationToken);
        if (shipment != null)
        {
            shipment.DriverProfileId = driverProfile.Id;
            // QR / deep-link claim is itself an explicit acceptance — skip the PendingDriverAcceptance gate.
            if (shipment.Status == ShipmentStatus.AwaitingDriver || shipment.Status == ShipmentStatus.PendingDriverAcceptance)
            {
                shipment.Status = ShipmentStatus.Ready;
                shipment.AssignedAt = null;
            }
        }
```

- [ ] **Step 2: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add HM.Infrastructure/Services/AuthService.cs
git commit -m "feat(auth): mark shipment Ready on QR/deep-link driver claim (implicit accept)"
```

---

## Task 8: Background timeout service for stuck assignments

**Files:**
- Create: `HM.Infrastructure/Services/PendingDriverAssignmentTimeoutService.cs`
- Modify: `HM.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Create the hosted service**

Create `HM.Infrastructure/Services/PendingDriverAssignmentTimeoutService.cs` with:

```csharp
using System.Text.Json;
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace HM.Infrastructure.Services;

/// <summary>
/// Polls every 60s for shipments stuck in PendingDriverAcceptance past the timeout window
/// (default 15 min). Reverts each to AwaitingDriver, clears DriverProfileId, and notifies
/// the truck account so they can pick a different driver.
/// </summary>
public sealed class PendingDriverAssignmentTimeoutService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan AcceptanceTimeout = TimeSpan.FromMinutes(15);

    private readonly IServiceProvider _services;
    private readonly ILogger<PendingDriverAssignmentTimeoutService> _logger;

    public PendingDriverAssignmentTimeoutService(IServiceProvider services, ILogger<PendingDriverAssignmentTimeoutService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessExpiredAssignmentsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PendingDriverAssignmentTimeoutService iteration failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Shutdown
            }
        }
    }

    private async Task ProcessExpiredAssignmentsAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();

        var cutoff = DateTime.UtcNow - AcceptanceTimeout;
        var expired = await db.Shipments
            .Where(s => s.Status == ShipmentStatus.PendingDriverAcceptance
                        && s.AssignedAt != null
                        && s.AssignedAt < cutoff)
            .ToListAsync(cancellationToken);

        if (expired.Count == 0) return;

        foreach (var shipment in expired)
        {
            shipment.Status = ShipmentStatus.AwaitingDriver;
            shipment.DriverProfileId = null;
            shipment.AssignedAt = null;
        }
        await db.SaveChangesAsync(cancellationToken);

        foreach (var shipment in expired)
        {
            try
            {
                var offer = await db.ShipmentOffers.FindAsync(new object[] { shipment.AcceptedOfferId }, cancellationToken);
                if (offer == null) continue;
                var truckAccount = await db.TruckAccounts.FindAsync(new object[] { offer.TruckAccountId }, cancellationToken);
                if (truckAccount == null) continue;
                var data = JsonSerializer.Serialize(new { shipmentId = shipment.Id });
                await notifications.SendNotificationAsync(
                    truckAccount.UserId,
                    "انتهت مهلة قبول السائق",
                    "لم يقم السائق بالرد خلال 15 دقيقة. يرجى تعيين سائق آخر.",
                    data,
                    true,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify truck account for timed-out shipment {ShipmentId}", shipment.Id);
            }
        }

        _logger.LogInformation("Timed out {Count} pending driver assignments", expired.Count);
    }
}
```

- [ ] **Step 2: Register the hosted service**

Open `HM.Infrastructure/DependencyInjection.cs`. Add the registration after the other `Scoped` registrations and before `services.Configure<FirebaseOptions>(...)`:

```csharp
        services.AddHostedService<PendingDriverAssignmentTimeoutService>();
```

The full block should look like:

```csharp
        services.AddScoped<JwtTokenGenerator>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IMerchantService, MerchantService>();
        services.AddScoped<ITruckService, TruckService>();
        services.AddScoped<IDriverService, DriverService>();
        services.AddScoped<ICurrentProfileAccessor, CurrentProfileAccessor>();
        services.AddScoped<INotificationService, NotificationService>();

        services.AddHostedService<PendingDriverAssignmentTimeoutService>();

        services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.SectionName));
```

- [ ] **Step 3: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 4: Commit**

```bash
git add HM.Infrastructure/Services/PendingDriverAssignmentTimeoutService.cs HM.Infrastructure/DependencyInjection.cs
git commit -m "feat(jobs): background service times out stale driver assignments after 15 min"
```

---

## Task 9: Truck account — list available drivers (`GET /api/truck/drivers`)

**Files:**
- Create: `HM.Application/Common/DTOs/Driver/DriverSummaryDto.cs`
- Modify: `HM.Application/Interfaces/Services/ITruckService.cs`
- Modify: `HM.Infrastructure/Services/TruckService.cs`
- Modify: `Hm.WebApi/Controllers/TruckController.cs`

- [ ] **Step 1: Create the DTO**

Create `HM.Application/Common/DTOs/Driver/DriverSummaryDto.cs`:

```csharp
namespace HM.Application.Common.DTOs.Driver;

/// <summary>Lightweight driver projection used by the truck account when picking a driver to assign.</summary>
public class DriverSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? AvatarUrl { get; set; }
    public bool IsVerified { get; set; }
    public bool HasNationalId { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 2: Add the contract method**

Open `HM.Application/Interfaces/Services/ITruckService.cs`. Add this method (place it next to the other shipment/driver-related methods, e.g. right after `GenerateDriverInvitationAsync`):

```csharp
    /// <summary>
    /// Returns a paginated list of all driver profiles in the system, for the truck account to pick from when assigning.
    /// Optional case-insensitive search on FullName.
    /// </summary>
    Task<PaginatedResult<DriverSummaryDto>> GetAvailableDriversAsync(string? search, PaginationRequest pagination, CancellationToken cancellationToken = default);
```

Add the using at the top of the file if not already present:

```csharp
using HM.Application.Common.DTOs.Driver;
```

- [ ] **Step 3: Implement in TruckService**

Open `HM.Infrastructure/Services/TruckService.cs`. Add this method (place it right after `GenerateDriverInvitationAsync`, around line ~434):

```csharp
    public async Task<PaginatedResult<DriverSummaryDto>> GetAvailableDriversAsync(string? search, PaginationRequest pagination, CancellationToken cancellationToken = default)
    {
        var query = _db.DriverProfiles.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(d => d.FullName.Contains(s));
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
```

Add the using at the top of `TruckService.cs` if not already present:

```csharp
using HM.Application.Common.DTOs.Driver;
```

- [ ] **Step 4: Add the controller endpoint**

Open `Hm.WebApi/Controllers/TruckController.cs`. Add this method (place it right after `InviteDriver`, around line ~159):

```csharp
    /// <summary>List all drivers in the system that the truck account can assign to a shipment. Supports search + pagination.</summary>
    [HttpGet("drivers")]
    public async Task<IActionResult> GetAvailableDrivers([FromQuery] string? search, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        var pagination = new PaginationRequest { PageNumber = pageNumber, PageSize = pageSize };
        var result = await _truckService.GetAvailableDriversAsync(search, pagination, cancellationToken);
        return Ok(result);
    }
```

- [ ] **Step 5: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add HM.Application/Common/DTOs/Driver/DriverSummaryDto.cs HM.Application/Interfaces/Services/ITruckService.cs HM.Infrastructure/Services/TruckService.cs Hm.WebApi/Controllers/TruckController.cs
git commit -m "feat(truck): add GET /api/truck/drivers list for driver assignment"
```

---

## Task 10: Merchant — list shipments for tracking (`GET /api/merchant/shipments`)

**Files:**
- Create: `HM.Application/Common/DTOs/Merchant/MerchantShipmentSummaryDto.cs`
- Modify: `HM.Application/Interfaces/Services/IMerchantService.cs`
- Modify: `HM.Infrastructure/Services/MerchantService.cs`
- Modify: `Hm.WebApi/Controllers/MerchantController.cs`

- [ ] **Step 1: Create the DTO**

Create `HM.Application/Common/DTOs/Merchant/MerchantShipmentSummaryDto.cs`:

```csharp
using HM.Domain.Enums;

namespace HM.Application.Common.DTOs.Merchant;

/// <summary>Summary of a merchant's accepted-and-active shipments for the tracking list.</summary>
public class MerchantShipmentSummaryDto
{
    public Guid ShipmentId { get; set; }
    public Guid ShipmentRequestId { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public ShipmentStatus Status { get; set; }
    public string PickupLocation { get; set; } = string.Empty;
    public string DropoffLocation { get; set; } = string.Empty;
    public string? PickupRegion { get; set; }
    public string? DropoffRegion { get; set; }
    public string? DriverName { get; set; }
    public string? DriverPhone { get; set; }
    public string? DriverAvatarUrl { get; set; }
    public string? TruckPlateNumber { get; set; }
    public TruckType? TruckType { get; set; }
    public decimal Price { get; set; }
    public double? CurrentLat { get; set; }
    public double? CurrentLng { get; set; }
    public DateTime? LastLocationUpdatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 2: Add the contract method**

Open `HM.Application/Interfaces/Services/IMerchantService.cs`. Add this method right after `CancelShipmentRequestAsync`:

```csharp
    /// <summary>
    /// Returns a paginated list of the merchant's shipments (accepted-offer onwards), with status,
    /// driver info, truck info, and last known location for the tracking list.
    /// Optional filter by ShipmentStatus.
    /// </summary>
    Task<PaginatedResult<MerchantShipmentSummaryDto>> GetMyShipmentsAsync(Guid userId, ShipmentStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default);
```

Add the using at the top of the file if not already present:

```csharp
using HM.Domain.Enums;
```

- [ ] **Step 3: Implement in MerchantService**

Open `HM.Infrastructure/Services/MerchantService.cs`. Add this method right before `GetTrackingAsync`:

```csharp
    public async Task<PaginatedResult<MerchantShipmentSummaryDto>> GetMyShipmentsAsync(Guid userId, ShipmentStatus? status, int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        var profileId = await ResolveMerchantProfileIdAsync(userId, cancellationToken);

        var requestIds = await _db.ShipmentRequests
            .AsNoTracking()
            .Where(r => r.MerchantProfileId == profileId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (requestIds.Count == 0)
        {
            return new PaginatedResult<MerchantShipmentSummaryDto>
            {
                Items = new List<MerchantShipmentSummaryDto>(),
                PageNumber = pageNumber < 1 ? 1 : pageNumber,
                PageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50),
                TotalCount = 0
            };
        }

        var query = _db.Shipments.AsNoTracking().Where(s => requestIds.Contains(s.ShipmentRequestId));
        if (status.HasValue)
            query = query.Where(s => s.Status == status.Value);

        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 10 : Math.Min(pageSize, 50);

        var total = await query.CountAsync(cancellationToken);
        var shipments = await query
            .OrderByDescending(s => s.StartedAt ?? DateTime.MinValue)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var requestLookup = await _db.ShipmentRequests
            .AsNoTracking()
            .Where(r => requestIds.Contains(r.Id))
            .ToDictionaryAsync(r => r.Id, cancellationToken);

        var offerIds = shipments.Select(s => s.AcceptedOfferId).Distinct().ToList();
        var offers = offerIds.Count > 0
            ? await _db.ShipmentOffers.AsNoTracking().Where(o => offerIds.Contains(o.Id)).ToDictionaryAsync(o => o.Id, cancellationToken)
            : new Dictionary<Guid, HM.Domain.Entities.ShipmentOffer>();

        var truckIds = shipments.Select(s => s.TruckId).Distinct().ToList();
        var trucks = truckIds.Count > 0
            ? await _db.Trucks.AsNoTracking().Where(t => truckIds.Contains(t.Id)).ToDictionaryAsync(t => t.Id, cancellationToken)
            : new Dictionary<Guid, HM.Domain.Entities.Truck>();

        var driverIds = shipments.Where(s => s.DriverProfileId.HasValue).Select(s => s.DriverProfileId!.Value).Distinct().ToList();
        var drivers = driverIds.Count > 0
            ? await _db.DriverProfiles.AsNoTracking().Where(d => driverIds.Contains(d.Id)).ToDictionaryAsync(d => d.Id, cancellationToken)
            : new Dictionary<Guid, HM.Domain.Entities.DriverProfile>();

        var driverUserIds = drivers.Values.Where(d => d.UserId.HasValue).Select(d => d.UserId!.Value).Distinct().ToList();
        var driverUsers = driverUserIds.Count > 0
            ? await _db.Users.AsNoTracking().Where(u => driverUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, cancellationToken)
            : new Dictionary<Guid, HM.Domain.Entities.User>();

        var regionIds = requestLookup.Values.SelectMany(r => new[] { r.PickupRegionId, r.DropoffRegionId }).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var regions = regionIds.Count > 0
            ? await _db.Regions.AsNoTracking().Where(r => regionIds.Contains(r.Id)).ToDictionaryAsync(r => r.Id, cancellationToken)
            : new Dictionary<Guid, HM.Domain.Entities.Region>();

        var items = shipments.Select(s =>
        {
            var req = requestLookup.GetValueOrDefault(s.ShipmentRequestId);
            var offer = offers.GetValueOrDefault(s.AcceptedOfferId);
            var truck = trucks.GetValueOrDefault(s.TruckId);
            var driver = s.DriverProfileId.HasValue ? drivers.GetValueOrDefault(s.DriverProfileId.Value) : null;
            var driverUser = driver != null && driver.UserId.HasValue ? driverUsers.GetValueOrDefault(driver.UserId.Value) : null;

            return new MerchantShipmentSummaryDto
            {
                ShipmentId = s.Id,
                ShipmentRequestId = s.ShipmentRequestId,
                RequestNumber = req?.RequestNumber ?? "",
                Status = s.Status,
                PickupLocation = req?.PickupLocation ?? "",
                DropoffLocation = req?.DropoffLocation ?? "",
                PickupRegion = req?.PickupRegionId.HasValue == true && regions.TryGetValue(req.PickupRegionId.Value, out var pr) ? pr.NameEn : null,
                DropoffRegion = req?.DropoffRegionId.HasValue == true && regions.TryGetValue(req.DropoffRegionId.Value, out var dr) ? dr.NameEn : null,
                DriverName = driver?.FullName,
                DriverPhone = driverUser?.PhoneNumber,
                DriverAvatarUrl = driver?.AvatarUrl,
                TruckPlateNumber = truck?.PlateNumber,
                TruckType = truck?.TruckType,
                Price = offer?.Price ?? 0,
                CurrentLat = s.CurrentLat,
                CurrentLng = s.CurrentLng,
                LastLocationUpdatedAt = s.LocationUpdatedAt,
                StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt,
                CreatedAt = req?.CreatedAt ?? DateTime.UtcNow
            };
        }).ToList();

        return new PaginatedResult<MerchantShipmentSummaryDto>
        {
            Items = items,
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalCount = total
        };
    }
```

- [ ] **Step 4: Add the controller endpoint**

Open `Hm.WebApi/Controllers/MerchantController.cs`. Add this method right before `GetTracking`:

```csharp
    /// <summary>List the merchant's shipments (accepted-offer onwards). Optional ShipmentStatus filter.</summary>
    [HttpGet("shipments")]
    public async Task<IActionResult> GetMyShipments(
        [FromQuery] ShipmentStatus? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var result = await _merchantService.GetMyShipmentsAsync(userId, status, pageNumber, pageSize, cancellationToken);
        return Ok(result);
    }
```

- [ ] **Step 5: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 6: Commit**

```bash
git add HM.Application/Common/DTOs/Merchant/MerchantShipmentSummaryDto.cs HM.Application/Interfaces/Services/IMerchantService.cs HM.Infrastructure/Services/MerchantService.cs Hm.WebApi/Controllers/MerchantController.cs
git commit -m "feat(merchant): add GET /api/merchant/shipments list for tracking"
```

---

## Task 11: SignalR hub authorization — let drivers (and merchants) join only their own shipment groups

**Files:**
- Modify: `Hm.WebApi/Hubs/ShipmentTrackingHub.cs`

The current hub lets any authenticated user join any `shipment-{id}` group. Now that we're enabling drivers as live-tracking subscribers (point #5), we tighten this so a user can only join groups for shipments where they are the merchant of the underlying request, the assigned driver, or the truck account that owns the accepted offer.

- [ ] **Step 1: Replace the hub with the authorized version**

Replace the entire contents of `Hm.WebApi/Hubs/ShipmentTrackingHub.cs` with:

```csharp
using HM.Application.Interfaces.Persistence;
using HM.Application.Interfaces.Services;
using HM.Domain.Enums;
using Hm.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Hm.WebApi.Hubs;

/// <summary>
/// SignalR hub for real-time shipment tracking.
/// Clients join/leave groups by shipment ID to receive LocationUpdated and StatusChanged events.
/// Group membership is authorized: caller must be the merchant of the shipment's request,
/// the assigned driver, or the truck account that owns the accepted offer.
/// </summary>
[Authorize]
public class ShipmentTrackingHub : Hub
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentProfileAccessor _profileAccessor;

    public ShipmentTrackingHub(IApplicationDbContext db, ICurrentProfileAccessor profileAccessor)
    {
        _db = db;
        _profileAccessor = profileAccessor;
    }

    /// <summary>Subscribe to real-time updates for a specific shipment.</summary>
    public async Task JoinShipmentTracking(Guid shipmentId)
    {
        var userId = Context.User?.GetUserId()
            ?? throw new HubException("User identifier not found.");

        if (!await IsAuthorizedForShipmentAsync(userId, shipmentId))
            throw new HubException("Not authorized to subscribe to this shipment.");

        await Groups.AddToGroupAsync(Context.ConnectionId, $"shipment-{shipmentId}");
    }

    /// <summary>Unsubscribe from updates for a specific shipment.</summary>
    public async Task LeaveShipmentTracking(Guid shipmentId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"shipment-{shipmentId}");
    }

    private async Task<bool> IsAuthorizedForShipmentAsync(Guid userId, Guid shipmentId)
    {
        var ct = Context.ConnectionAborted;
        var shipment = await _db.Shipments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == shipmentId, ct);
        if (shipment == null) return false;

        // Driver branch
        var driverProfileId = await _profileAccessor.GetDriverProfileIdAsync(userId, ct);
        if (driverProfileId.HasValue && shipment.DriverProfileId == driverProfileId.Value)
            return true;

        // Truck account branch
        var truckAccountId = await _profileAccessor.GetTruckAccountIdAsync(userId, ct);
        if (truckAccountId.HasValue)
        {
            var offer = await _db.ShipmentOffers.AsNoTracking().FirstOrDefaultAsync(o => o.Id == shipment.AcceptedOfferId, ct);
            if (offer != null && offer.TruckAccountId == truckAccountId.Value)
                return true;
        }

        // Merchant branch
        var request = await _db.ShipmentRequests.AsNoTracking().FirstOrDefaultAsync(r => r.Id == shipment.ShipmentRequestId, ct);
        if (request != null)
        {
            var merchantProfile = await _db.MerchantProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.Id == request.MerchantProfileId, ct);
            if (merchantProfile != null && merchantProfile.UserId == userId)
                return true;
        }

        return false;
    }
}
```

> Note: `ICurrentProfileAccessor` already exists with `GetDriverProfileIdAsync(userId, ct)` and `GetTruckAccountIdAsync(userId, ct)` — confirm by reading `HM.Application/Interfaces/Services/ICurrentProfileAccessor.cs` before continuing. If the signatures differ, adapt the calls to match.

- [ ] **Step 2: Build**

Run: `dotnet build HM.sln`
Expected: `Build succeeded. 0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add Hm.WebApi/Hubs/ShipmentTrackingHub.cs
git commit -m "feat(realtime): authorize SignalR group joins by shipment relationship"
```

---

## Task 12: Update Postman collection

**Files:**
- Modify: `HM.postman_collection.json`

- [ ] **Step 1: Read the current collection structure**

Run: `head -50 HM.postman_collection.json` (or open in editor) to inspect the existing folder structure (Auth / Driver / DriverShipments / Location / Merchant / Notification / Truck).

- [ ] **Step 2: Add the four new requests**

Edit `HM.postman_collection.json`. Add these requests under the matching folders. Match the existing JSON shape (use one of the sibling requests as a template — copy its `request.auth`, headers, and `response: []`).

Under **Truck** folder, after `Invite Driver to Shipment`:

```json
{
  "name": "Get Available Drivers",
  "request": {
    "method": "GET",
    "header": [],
    "url": {
      "raw": "{{baseUrl}}/api/truck/drivers?pageNumber=1&pageSize=10",
      "host": ["{{baseUrl}}"],
      "path": ["api", "truck", "drivers"],
      "query": [
        { "key": "pageNumber", "value": "1" },
        { "key": "pageSize", "value": "10" },
        { "key": "search", "value": "", "disabled": true }
      ]
    }
  },
  "response": []
}
```

Under **Merchant** folder, after `Cancel Shipment Request` (and before `Get Shipment Tracking`):

```json
{
  "name": "Get My Shipments",
  "request": {
    "method": "GET",
    "header": [],
    "url": {
      "raw": "{{baseUrl}}/api/merchant/shipments?pageNumber=1&pageSize=10",
      "host": ["{{baseUrl}}"],
      "path": ["api", "merchant", "shipments"],
      "query": [
        { "key": "pageNumber", "value": "1" },
        { "key": "pageSize", "value": "10" },
        { "key": "status", "value": "InTransit", "disabled": true }
      ]
    }
  },
  "response": []
}
```

Under **DriverShipments** folder (alongside `Start Trip`, `Mark Arrived`, etc.), add two requests:

```json
{
  "name": "Accept Assignment",
  "request": {
    "method": "POST",
    "header": [{ "key": "Content-Type", "value": "application/json" }],
    "body": { "mode": "raw", "raw": "" },
    "url": {
      "raw": "{{baseUrl}}/api/driver/shipments/{{shipmentId}}/accept",
      "host": ["{{baseUrl}}"],
      "path": ["api", "driver", "shipments", "{{shipmentId}}", "accept"]
    }
  },
  "response": []
},
{
  "name": "Reject Assignment",
  "request": {
    "method": "POST",
    "header": [{ "key": "Content-Type", "value": "application/json" }],
    "body": { "mode": "raw", "raw": "" },
    "url": {
      "raw": "{{baseUrl}}/api/driver/shipments/{{shipmentId}}/reject",
      "host": ["{{baseUrl}}"],
      "path": ["api", "driver", "shipments", "{{shipmentId}}", "reject"]
    }
  },
  "response": []
}
```

Make sure to put a comma between the new request objects and the existing siblings — bad JSON will silently break the import.

- [ ] **Step 3: Validate JSON**

Run from repo root:

```bash
node -e "JSON.parse(require('fs').readFileSync('HM.postman_collection.json'))" && echo OK
```

Expected: `OK`. If `Unexpected token` or similar, fix the JSON before proceeding.

- [ ] **Step 4: Commit**

```bash
git add HM.postman_collection.json
git commit -m "docs(postman): add new merchant/truck/driver-acceptance endpoints"
```

---

## Task 13: End-to-end smoke test

This task has no code changes — it is the final verification gate before declaring the plan complete.

- [ ] **Step 1: Apply the migration**

Run: `dotnet ef database update --project HM.Infrastructure --startup-project Hm.WebApi`
Expected: `Done.` and the new column visible in the `Shipments` table:

```bash
psql -d hm -c "\d \"Shipments\"" | grep AssignedAt
```
Expected: a row matching `AssignedAt | timestamp with time zone`.

- [ ] **Step 2: Start the API**

Run: `dotnet run --project Hm.WebApi --launch-profile https`
Expected: log line `Now listening on: https://localhost:5xxx` and `Application started`.

- [ ] **Step 3: Smoke-test the merchant shipments list**

Get a merchant JWT (login flow), then:

```bash
curl -sk -H "Authorization: Bearer $MERCHANT_TOKEN" \
  "https://localhost:5xxx/api/merchant/shipments?pageNumber=1&pageSize=10" | jq .
```
Expected: a JSON object with `items`, `pageNumber: 1`, `pageSize: 10`, `totalCount: <int>`. If the merchant has no shipments yet, `items` is `[]` and `totalCount: 0`.

- [ ] **Step 4: Smoke-test the truck drivers list**

Get a truck-account JWT, then:

```bash
curl -sk -H "Authorization: Bearer $TRUCK_TOKEN" \
  "https://localhost:5xxx/api/truck/drivers?pageNumber=1&pageSize=10" | jq .
```
Expected: `items` array of `{ id, fullName, phoneNumber, ... }`, paginated.

- [ ] **Step 5: Smoke-test the driver acceptance flow**

Pre-condition: a shipment exists in `AwaitingDriver` status. Assign a driver via the truck account:

```bash
curl -sk -X POST -H "Authorization: Bearer $TRUCK_TOKEN" -H "Content-Type: application/json" \
  -d "{\"driverProfileId\":\"$DRIVER_PROFILE_ID\"}" \
  "https://localhost:5xxx/api/truck/shipments/$SHIPMENT_ID/assign-driver" | jq .status
```
Expected: `"PendingDriverAcceptance"`.

Then accept as the driver:

```bash
curl -sk -X POST -H "Authorization: Bearer $DRIVER_TOKEN" \
  "https://localhost:5xxx/api/driver/shipments/$SHIPMENT_ID/accept" | jq .status
```
Expected: `"Ready"`.

Repeat the assign step on a fresh shipment, then reject:

```bash
curl -sk -X POST -H "Authorization: Bearer $DRIVER_TOKEN" \
  "https://localhost:5xxx/api/driver/shipments/$SHIPMENT_ID/reject" | jq .status
```
Expected: `"AwaitingDriver"`.

- [ ] **Step 6: Smoke-test the timeout job (optional, slow)**

Assign a driver but don't accept/reject. Wait 16 minutes. Run:

```bash
curl -sk -H "Authorization: Bearer $MERCHANT_TOKEN" \
  "https://localhost:5xxx/api/merchant/shipments/$SHIPMENT_ID/tracking" | jq .status
```
Expected: `"AwaitingDriver"`. The truck account should also have received a notification — verify via:

```bash
curl -sk -H "Authorization: Bearer $TRUCK_TOKEN" \
  "https://localhost:5xxx/api/Notifications?pageNumber=1&pageSize=5" | jq '.[0].title'
```
Expected: `"انتهت مهلة قبول السائق"`.

For a faster check, edit `PendingDriverAssignmentTimeoutService.cs` temporarily and set `AcceptanceTimeout = TimeSpan.FromSeconds(30)` and `PollInterval = TimeSpan.FromSeconds(10)`, exercise the flow, then revert. Don't commit the temporary values.

- [ ] **Step 7: Smoke-test SignalR group authorization**

Use a merchant JWT and connect to `/hubs/shipment-tracking`. Call `JoinShipmentTracking` with a shipmentId belonging to a different merchant. Expected: `HubException: Not authorized to subscribe to this shipment.`. With your own shipmentId, expected: success.

- [ ] **Step 8: Final commit (if any cleanups)**

If Task 13 didn't require code changes, no commit is needed. Otherwise:

```bash
git add -p
git commit -m "fix: address smoke-test findings"
```

---

## Self-Review

Before declaring this plan complete I cross-checked it against the four user-confirmed requests and the architectural decisions:

**Spec coverage:**
- ✅ Request 1 (merchant lists own shipments) — Task 10
- ✅ Request 2 (truck lists drivers) — Task 9
- ✅ Request 3 (keep `/api/truck/shipments/nearby`) — no change, deliberately untouched
- ✅ Request 4 (driver acceptance) — Tasks 1–8 (enum + field + service + endpoints + auth-invitation auto-accept + timeout job)
- ✅ Request 5 (driver real-time tracking) — Task 11 (hub authorization), driver-side `/tracking` endpoint already existed in `DriverShipmentsController.GetTracking`

**Placeholder scan:** No "TBD" / "TODO" / "implement appropriate validation" anywhere. Every step has runnable code.

**Type consistency:**
- `DriverSummaryDto` referenced in `ITruckService` (Task 9 Step 2) matches the file created in Task 9 Step 1.
- `MerchantShipmentSummaryDto` referenced in `IMerchantService` (Task 10 Step 2) matches the file created in Task 10 Step 1.
- `AcceptAssignmentAsync` / `RejectAssignmentAsync` signatures match between `IDriverService` (Task 4), `DriverService` (Task 5), and the controller (Task 6) — all use `(Guid driverUserId, Guid shipmentId, CancellationToken)`.
- The new enum value `PendingDriverAcceptance` (Task 1) is referenced consistently in Tasks 3, 5, 7, 8, and 13.
- `Shipment.AssignedAt` (Task 2) is set in Task 3, cleared in Tasks 5 and 7, and queried in Task 8 — all use the same property name.

**One residual decision the executor may need to revisit:** the timeout window is hard-coded at 15 minutes inside `PendingDriverAssignmentTimeoutService`. If the user later wants this configurable, it should move to `appsettings.json` under a section like `DriverAssignment:TimeoutMinutes`. Out of scope for this plan.
