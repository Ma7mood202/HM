# Admin Dashboard v1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build v1 of the HM admin dashboard — modules 11 (Auth), 1 (Dashboard), 2 (Users), 3 (Trucks), 4 (Shipments) — as a new `HM.AdminPanel` ASP.NET Core 8 MVC project alongside `Hm.WebApi`.

**Architecture:** New MVC project in the same solution. References `HM.Application` + `HM.Infrastructure` (transitively `HM.Domain`). Cookie auth with new `Admin` parent role and `SuperAdmin`/`Support`/`ReadOnly` sub-roles on existing ASP.NET Identity. AdminLTE 3 UI. Global audit filter writes `AdminAuditLog` rows on every state-changing request. Live shipment map connects to existing `/hubs/shipment-tracking` SignalR hub on `Hm.WebApi`.

**Tech Stack:** ASP.NET Core 8 MVC, Razor Views, AdminLTE 3, jQuery DataTables, Chart.js, Leaflet, `@microsoft/signalr` browser client, EF Core 8 (Npgsql), ASP.NET Identity (`IdentityUser<Guid>`, `IdentityRole<Guid>`).

**Spec:** `docs/superpowers/specs/2026-05-24-admin-dashboard-design.md`

**Branch:** `feat/admin-dashboard` (already created)

**No automated tests:** project has no test infrastructure. Each task ends with `dotnet build` + manual smoke verification + commit.

---

## Refinements from the spec

While writing this plan, two refinements were locked in (both stay compatible with the approved spec):

- **`AdminAuditLog.AdminUserId` and `AdminLoginAttempt`-style FKs use `Guid`**, not `string`. Existing Identity uses `IdentityUser<Guid>`, so `Guid` is the consistent key type.
- **`IsBlocked` / `BlockedAt` / `BlockedReason` are added to `HM.Domain.Entities.User`** (the domain user), not `ApplicationUser`. Reason: `ApplicationUser` is intentionally empty (auth-only); business state belongs on the domain user. The login flow checks the domain user's `IsBlocked` flag by id (they share the same `Guid`).

---

## File map

```
HM.Domain/
├── Entities/
│   ├── AdminAuditLog.cs           NEW
│   ├── AdminLoginAttempt.cs       NEW
│   ├── AppSetting.cs              NEW
│   ├── User.cs                    MODIFY  (+IsBlocked, +BlockedAt, +BlockedReason)
│   └── Truck.cs                   MODIFY  (+ApprovalStatus, +RejectionReason)
└── Enums/
    └── TruckApprovalStatus.cs     NEW

HM.Infrastructure/
├── Configurations/
│   ├── AdminAuditLogConfiguration.cs       NEW
│   ├── AdminLoginAttemptConfiguration.cs   NEW
│   ├── AppSettingConfiguration.cs          NEW
│   ├── UserConfiguration.cs                MODIFY  (map new columns)
│   └── TruckConfiguration.cs               MODIFY  (map new columns)
├── Data/
│   ├── ApplicationDbContext.cs    MODIFY  (+3 DbSets)
│   └── DbSeeder.cs                MODIFY  (+admin roles, +seed SuperAdmin)
└── Migrations/                    NEW     (AddAdminPanelTables)

HM.AdminPanel/                     NEW PROJECT
├── HM.AdminPanel.csproj
├── Program.cs
├── appsettings.json
├── appsettings.Development.json
├── Properties/launchSettings.json
├── Controllers/
│   ├── AccountController.cs
│   ├── ErrorController.cs
│   ├── DashboardController.cs
│   ├── MerchantsController.cs
│   ├── DriversController.cs
│   ├── TruckAccountsController.cs
│   ├── TrucksController.cs
│   ├── ShipmentsController.cs
│   └── ShipmentRequestsController.cs
├── ViewModels/
│   ├── Account/LoginVm.cs
│   ├── Dashboard/DashboardVm.cs, LiveMapVm.cs
│   ├── Common/PagedResult.cs, FilterBase.cs
│   ├── Merchants/MerchantListVm.cs, MerchantFilterVm.cs, MerchantDetailVm.cs
│   ├── Drivers/...
│   ├── TruckAccounts/...
│   ├── Trucks/TruckListVm.cs, TruckFilterVm.cs, TruckDetailVm.cs
│   ├── Shipments/ShipmentListVm.cs, ShipmentFilterVm.cs, ShipmentDetailVm.cs
│   └── ShipmentRequests/...
├── Authorization/
│   ├── AdminRoles.cs
│   ├── AdminPolicies.cs
│   └── AuditActionFilter.cs
├── Services/
│   ├── IAdminAuditLogger.cs + AdminAuditLogger.cs
│   ├── IDashboardQueryService.cs + DashboardQueryService.cs
│   └── LoginThrottleService.cs
├── Extensions/
│   ├── ServiceCollectionExtensions.cs
│   └── HttpContextExtensions.cs
├── Views/
│   ├── _ViewImports.cshtml, _ViewStart.cshtml
│   ├── Shared/_Layout.cshtml, _LoginLayout.cshtml, _Sidebar.cshtml,
│   │         _Pager.cshtml, _ValidationScriptsPartial.cshtml,
│   │         Error/NotFound.cshtml, AccessDenied.cshtml, GenericError.cshtml
│   ├── Account/Login.cshtml
│   ├── Dashboard/Index.cshtml, LiveMap.cshtml
│   ├── Merchants/Index.cshtml, Details.cshtml
│   ├── Drivers/Index.cshtml, Details.cshtml
│   ├── TruckAccounts/Index.cshtml, Details.cshtml
│   ├── Trucks/Index.cshtml, Details.cshtml
│   ├── Shipments/Index.cshtml, Details.cshtml
│   └── ShipmentRequests/Index.cshtml
└── wwwroot/
    ├── adminlte/        (vendored: css + js)
    ├── leaflet/         (vendored)
    ├── signalr/         (vendored)
    ├── lib/             (jquery, datatables, chart.js, sweetalert2)
    └── css/site.css, js/site.js

scripts/
└── deploy.sh                       MODIFY (publish + restart hm-admin)

HM.sln                              MODIFY (add HM.AdminPanel)
Hm.WebApi/appsettings.json          MODIFY (+AdminPanel:SeedAdmin section)
```

---

## Phase A — Foundation: entities, migration, role seeding

### Task A1: Add `IsBlocked` fields to domain `User`

**Files:**
- Modify: `HM.Domain/Entities/User.cs`

- [ ] **Step 1: Add the three new properties at the bottom of `User`**

```csharp
// HM.Domain/Entities/User.cs
using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class User
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserType UserType { get; set; }
    public bool IsActive { get; set; }
    public bool IsOtpVerified { get; set; }
    public string? OtpCode { get; set; }
    public DateTime? OtpExpiresAt { get; set; }
    public OtpPurpose OtpPurpose { get; set; }
    public DateTime CreatedAt { get; set; }

    // Admin-panel additions
    public bool      IsBlocked     { get; set; }
    public DateTime? BlockedAt     { get; set; }
    public string?   BlockedReason { get; set; }
}
```

- [ ] **Step 2: Build the Domain project**

Run: `dotnet build HM.Domain/HM.Domain.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`.

- [ ] **Step 3: Commit**

```bash
git add HM.Domain/Entities/User.cs
git commit -m "feat(domain): add IsBlocked/BlockedAt/BlockedReason on User"
```

---

### Task A2: Add `TruckApprovalStatus` enum + extend `Truck`

**Files:**
- Create: `HM.Domain/Enums/TruckApprovalStatus.cs`
- Modify: `HM.Domain/Entities/Truck.cs`

- [ ] **Step 1: Create the enum**

```csharp
// HM.Domain/Enums/TruckApprovalStatus.cs
namespace HM.Domain.Enums;

public enum TruckApprovalStatus
{
    Pending,
    Approved,
    Rejected
}
```

- [ ] **Step 2: Extend `Truck`**

```csharp
// HM.Domain/Entities/Truck.cs
using HM.Domain.Enums;

namespace HM.Domain.Entities;

public class Truck
{
    public Guid Id { get; set; }
    public Guid TruckAccountId { get; set; }
    public TruckType TruckType { get; set; }
    public TruckBodyType? BodyType { get; set; }
    public decimal MaxWeight { get; set; }
    public string PlateNumber { get; set; } = string.Empty;
    public bool IsActive { get; set; }

    // Admin-panel additions
    public TruckApprovalStatus ApprovalStatus { get; set; }
    public string? RejectionReason { get; set; }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build HM.Domain/HM.Domain.csproj
git add HM.Domain/Entities/Truck.cs HM.Domain/Enums/TruckApprovalStatus.cs
git commit -m "feat(domain): add Truck.ApprovalStatus + RejectionReason"
```

---

### Task A3: Create `AdminAuditLog`, `AdminLoginAttempt`, `AppSetting` entities

**Files:**
- Create: `HM.Domain/Entities/AdminAuditLog.cs`
- Create: `HM.Domain/Entities/AdminLoginAttempt.cs`
- Create: `HM.Domain/Entities/AppSetting.cs`

- [ ] **Step 1: Create `AdminAuditLog`**

```csharp
// HM.Domain/Entities/AdminAuditLog.cs
namespace HM.Domain.Entities;

public class AdminAuditLog
{
    public Guid     Id          { get; set; }
    public Guid     AdminUserId { get; set; }
    public string   AdminEmail  { get; set; } = string.Empty;
    public string   Action      { get; set; } = string.Empty;
    public string   EntityType  { get; set; } = string.Empty;
    public string?  EntityId    { get; set; }
    public string?  Details     { get; set; }
    public string?  IpAddress   { get; set; }
    public DateTime CreatedAt   { get; set; }
}
```

- [ ] **Step 2: Create `AdminLoginAttempt`**

```csharp
// HM.Domain/Entities/AdminLoginAttempt.cs
namespace HM.Domain.Entities;

public class AdminLoginAttempt
{
    public Guid     Id        { get; set; }
    public string   Email     { get; set; } = string.Empty;
    public bool     Success   { get; set; }
    public string?  IpAddress { get; set; }
    public string?  UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 3: Create `AppSetting`**

```csharp
// HM.Domain/Entities/AppSetting.cs
namespace HM.Domain.Entities;

public class AppSetting
{
    public string   Key         { get; set; } = string.Empty;
    public string   Value       { get; set; } = string.Empty;
    public string?  Description { get; set; }
    public DateTime UpdatedAt   { get; set; }
    public Guid?    UpdatedBy   { get; set; }
}
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build HM.Domain/HM.Domain.csproj
git add HM.Domain/Entities/AdminAuditLog.cs HM.Domain/Entities/AdminLoginAttempt.cs HM.Domain/Entities/AppSetting.cs
git commit -m "feat(domain): add AdminAuditLog, AdminLoginAttempt, AppSetting"
```

---

### Task A4: EF configurations for new entities

**Files:**
- Create: `HM.Infrastructure/Configurations/AdminAuditLogConfiguration.cs`
- Create: `HM.Infrastructure/Configurations/AdminLoginAttemptConfiguration.cs`
- Create: `HM.Infrastructure/Configurations/AppSettingConfiguration.cs`
- Modify: `HM.Infrastructure/Configurations/UserConfiguration.cs` (add new column mappings)
- Modify: `HM.Infrastructure/Configurations/TruckConfiguration.cs` (add new column mappings)

- [ ] **Step 1: `AdminAuditLogConfiguration`**

```csharp
// HM.Infrastructure/Configurations/AdminAuditLogConfiguration.cs
using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class AdminAuditLogConfiguration : IEntityTypeConfiguration<AdminAuditLog>
{
    public void Configure(EntityTypeBuilder<AdminAuditLog> builder)
    {
        builder.ToTable("AdminAuditLogs");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.AdminEmail).IsRequired().HasMaxLength(256);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(128);
        builder.Property(x => x.EntityType).IsRequired().HasMaxLength(128);
        builder.Property(x => x.EntityId).HasMaxLength(128);
        builder.Property(x => x.IpAddress).HasMaxLength(64);

        builder.HasIndex(x => x.AdminUserId);
        builder.HasIndex(x => x.CreatedAt);
        builder.HasIndex(x => new { x.EntityType, x.EntityId });
    }
}
```

- [ ] **Step 2: `AdminLoginAttemptConfiguration`**

```csharp
// HM.Infrastructure/Configurations/AdminLoginAttemptConfiguration.cs
using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class AdminLoginAttemptConfiguration : IEntityTypeConfiguration<AdminLoginAttempt>
{
    public void Configure(EntityTypeBuilder<AdminLoginAttempt> builder)
    {
        builder.ToTable("AdminLoginAttempts");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.IpAddress).HasMaxLength(64);
        builder.Property(x => x.UserAgent).HasMaxLength(512);

        builder.HasIndex(x => x.Email);
        builder.HasIndex(x => x.CreatedAt);
    }
}
```

- [ ] **Step 3: `AppSettingConfiguration`**

```csharp
// HM.Infrastructure/Configurations/AppSettingConfiguration.cs
using HM.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HM.Infrastructure.Configurations;

public class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("AppSettings");
        builder.HasKey(x => x.Key);
        builder.Property(x => x.Key).HasMaxLength(128);
        builder.Property(x => x.Value).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(512);
    }
}
```

- [ ] **Step 4: Add new property mappings to `UserConfiguration`** — open it and inside `Configure(...)` append:

```csharp
builder.Property(u => u.IsBlocked).IsRequired().HasDefaultValue(false);
builder.Property(u => u.BlockedReason).HasMaxLength(512);
```

- [ ] **Step 5: Add new property mappings to `TruckConfiguration`** — append:

```csharp
builder.Property(t => t.ApprovalStatus)
    .HasConversion<string>()
    .HasMaxLength(16)
    .IsRequired()
    .HasDefaultValue(TruckApprovalStatus.Pending);

builder.Property(t => t.RejectionReason).HasMaxLength(512);
```

- [ ] **Step 6: Build + commit**

```bash
dotnet build HM.Infrastructure/HM.Infrastructure.csproj
git add HM.Infrastructure/Configurations/
git commit -m "feat(infra): EF configs for AdminAuditLog/LoginAttempt/AppSetting + User/Truck"
```

---

### Task A5: Wire new `DbSet`s into `ApplicationDbContext`

**Files:**
- Modify: `HM.Infrastructure/Data/ApplicationDbContext.cs`

- [ ] **Step 1: Add three new DbSets after the existing ones**

```csharp
public DbSet<AdminAuditLog>     AdminAuditLogs     => Set<AdminAuditLog>();
public DbSet<AdminLoginAttempt> AdminLoginAttempts => Set<AdminLoginAttempt>();
public DbSet<AppSetting>        AppSettings        => Set<AppSetting>();
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build HM.Infrastructure/HM.Infrastructure.csproj
git add HM.Infrastructure/Data/ApplicationDbContext.cs
git commit -m "feat(infra): expose admin DbSets in ApplicationDbContext"
```

---

### Task A6: Seed admin roles + default SuperAdmin in `DbSeeder`

**Files:**
- Modify: `HM.Infrastructure/Data/DbSeeder.cs`
- Modify: `Hm.WebApi/appsettings.json` (add seed credentials)

- [ ] **Step 1: Add seed credentials section to `Hm.WebApi/appsettings.json`** — insert under the root object:

```jsonc
"AdminPanel": {
  "SeedAdmin": {
    "Email": "admin@hm.local",
    "Password": "ChangeMe!2026"
  }
}
```

- [ ] **Step 2: Replace `DbSeeder.SeedAsync` body** with the extended version that seeds admin roles and a default SuperAdmin:

```csharp
// HM.Infrastructure/Data/DbSeeder.cs
using HM.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace HM.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("HM.Infrastructure.Data.DbSeeder");

        var roleManager = scope.ServiceProvider.GetService<RoleManager<IdentityRole<Guid>>>();
        var userManager = scope.ServiceProvider.GetService<UserManager<ApplicationUser>>();
        var config      = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        if (roleManager is null) return;

        var roleNames = new[] { "Merchant", "TruckAccount", "Driver",
                                "Admin", "SuperAdmin", "Support", "ReadOnly" };
        foreach (var name in roleNames)
        {
            if (await roleManager.RoleExistsAsync(name)) continue;
            await roleManager.CreateAsync(new IdentityRole<Guid>(name));
            logger.LogInformation("Created role: {Role}", name);
        }

        if (userManager is null) return;

        var seedEmail = config["AdminPanel:SeedAdmin:Email"];
        var seedPwd   = config["AdminPanel:SeedAdmin:Password"];
        if (string.IsNullOrWhiteSpace(seedEmail) || string.IsNullOrWhiteSpace(seedPwd))
        {
            logger.LogWarning("AdminPanel:SeedAdmin not configured; skipping admin seed.");
            return;
        }

        var existing = await userManager.FindByEmailAsync(seedEmail);
        if (existing is not null) return;

        var admin = new ApplicationUser
        {
            Id             = Guid.NewGuid(),
            UserName       = seedEmail,
            Email          = seedEmail,
            EmailConfirmed = true
        };
        var create = await userManager.CreateAsync(admin, seedPwd);
        if (!create.Succeeded)
        {
            logger.LogError("Failed to seed SuperAdmin: {Errors}",
                string.Join("; ", create.Errors.Select(e => e.Description)));
            return;
        }
        await userManager.AddToRolesAsync(admin, new[] { "Admin", "SuperAdmin" });
        logger.LogInformation("Seeded SuperAdmin: {Email}", seedEmail);
    }
}
```

- [ ] **Step 3: Build entire solution**

Run: `dotnet build HM.sln`
Expected: `Build succeeded`.

- [ ] **Step 4: Commit**

```bash
git add HM.Infrastructure/Data/DbSeeder.cs Hm.WebApi/appsettings.json
git commit -m "feat(infra): seed Admin/SuperAdmin/Support/ReadOnly roles + default SuperAdmin"
```

---

### Task A7: Create the EF migration

**Files:**
- Create: `HM.Infrastructure/Migrations/*_AddAdminPanelTables.cs` (auto-generated)

- [ ] **Step 1: Generate the migration**

Run from solution root:
```bash
dotnet ef migrations add AddAdminPanelTables --project HM.Infrastructure --startup-project Hm.WebApi
```
Expected: New files under `HM.Infrastructure/Migrations/`. No errors.

- [ ] **Step 2: Open the generated `*_AddAdminPanelTables.cs`** and verify it contains:
- `CreateTable` for `AdminAuditLogs`, `AdminLoginAttempts`, `AppSettings`
- `AddColumn` for `Users.IsBlocked`, `Users.BlockedAt`, `Users.BlockedReason`
- `AddColumn` for `Trucks.ApprovalStatus`, `Trucks.RejectionReason`
- Indexes from configurations

If any are missing, the config files are wrong — fix them and re-run migration.

- [ ] **Step 3: Build to confirm migration compiles**

Run: `dotnet build HM.sln`

- [ ] **Step 4: Commit**

```bash
git add HM.Infrastructure/Migrations/
git commit -m "feat(infra): EF migration AddAdminPanelTables"
```

---

## Phase B — Project scaffolding

### Task B1: Create the `HM.AdminPanel` MVC project

**Files:**
- Create: `HM.AdminPanel/HM.AdminPanel.csproj`
- Create: `HM.AdminPanel/Program.cs` (placeholder)
- Create: `HM.AdminPanel/appsettings.json`
- Create: `HM.AdminPanel/appsettings.Development.json`
- Create: `HM.AdminPanel/Properties/launchSettings.json`
- Modify: `HM.sln`

- [ ] **Step 1: Create project via dotnet CLI** (run from solution root)

```bash
dotnet new mvc -n HM.AdminPanel -f net8.0 -o HM.AdminPanel
```

- [ ] **Step 2: Add project references**

```bash
dotnet add HM.AdminPanel/HM.AdminPanel.csproj reference HM.Application/HM.Application.csproj HM.Infrastructure/HM.Infrastructure.csproj
```

- [ ] **Step 3: Add to solution**

```bash
dotnet sln HM.sln add HM.AdminPanel/HM.AdminPanel.csproj
```

- [ ] **Step 4: Replace `HM.AdminPanel/appsettings.json`** with:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=127.0.0.1;Port=5432;Database=HM;Username=fustani_user;Password=Fustani.DB.P@$$w0rd;SSL Mode=Require;Trust Server Certificate=true;Timeout=30;Command Timeout=30"
  },
  "AdminPanel": {
    "WebApiBaseUrl": "https://localhost:7000",
    "SignalRHubUrl": "https://localhost:7000/hubs/shipment-tracking"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

- [ ] **Step 5: Configure launch profile** — replace `HM.AdminPanel/Properties/launchSettings.json` with:

```json
{
  "profiles": {
    "HM.AdminPanel": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7050;http://localhost:5050",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

- [ ] **Step 6: Verify it builds and starts**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
```
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add HM.AdminPanel/ HM.sln
git commit -m "feat(admin): scaffold HM.AdminPanel MVC project"
```

---

### Task B2: Authorization primitives

**Files:**
- Create: `HM.AdminPanel/Authorization/AdminRoles.cs`
- Create: `HM.AdminPanel/Authorization/AdminPolicies.cs`

- [ ] **Step 1: `AdminRoles.cs`**

```csharp
// HM.AdminPanel/Authorization/AdminRoles.cs
namespace HM.AdminPanel.Authorization;

public static class AdminRoles
{
    public const string Admin      = "Admin";
    public const string SuperAdmin = "SuperAdmin";
    public const string Support    = "Support";
    public const string ReadOnly   = "ReadOnly";
}
```

- [ ] **Step 2: `AdminPolicies.cs`**

```csharp
// HM.AdminPanel/Authorization/AdminPolicies.cs
namespace HM.AdminPanel.Authorization;

public static class AdminPolicies
{
    public const string RequireAdmin       = "RequireAdmin";
    public const string RequireSuperAdmin  = "RequireSuperAdmin";
    public const string RequireWriteAccess = "RequireWriteAccess";
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Authorization/
git commit -m "feat(admin): AdminRoles + AdminPolicies"
```

---

### Task B3: `IAdminAuditLogger` + `AdminAuditLogger`

**Files:**
- Create: `HM.AdminPanel/Services/IAdminAuditLogger.cs`
- Create: `HM.AdminPanel/Services/AdminAuditLogger.cs`

- [ ] **Step 1: Interface**

```csharp
// HM.AdminPanel/Services/IAdminAuditLogger.cs
namespace HM.AdminPanel.Services;

public interface IAdminAuditLogger
{
    Task LogAsync(
        Guid    adminUserId,
        string  adminEmail,
        string  action,
        string  entityType,
        string? entityId,
        string? details,
        string? ipAddress,
        CancellationToken ct = default);
}
```

- [ ] **Step 2: Implementation**

```csharp
// HM.AdminPanel/Services/AdminAuditLogger.cs
using HM.Domain.Entities;
using HM.Infrastructure.Data;

namespace HM.AdminPanel.Services;

public class AdminAuditLogger : IAdminAuditLogger
{
    private readonly ApplicationDbContext _db;
    public AdminAuditLogger(ApplicationDbContext db) => _db = db;

    public async Task LogAsync(
        Guid adminUserId, string adminEmail, string action, string entityType,
        string? entityId, string? details, string? ipAddress,
        CancellationToken ct = default)
    {
        _db.AdminAuditLogs.Add(new AdminAuditLog
        {
            Id          = Guid.NewGuid(),
            AdminUserId = adminUserId,
            AdminEmail  = adminEmail,
            Action      = action,
            EntityType  = entityType,
            EntityId    = entityId,
            Details     = details,
            IpAddress   = ipAddress,
            CreatedAt   = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Services/
git commit -m "feat(admin): AdminAuditLogger service"
```

---

### Task B4: `AuditActionFilter`

**Files:**
- Create: `HM.AdminPanel/Authorization/AuditActionFilter.cs`

- [ ] **Step 1: Create the filter**

```csharp
// HM.AdminPanel/Authorization/AuditActionFilter.cs
using System.Security.Claims;
using System.Text.Json;
using HM.AdminPanel.Services;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HM.AdminPanel.Authorization;

public class AuditActionFilter : IAsyncActionFilter
{
    private static readonly HashSet<string> WriteVerbs =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "DELETE", "PATCH" };

    private readonly IAdminAuditLogger _logger;

    public AuditActionFilter(IAdminAuditLogger logger) => _logger = logger;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var verb = context.HttpContext.Request.Method;
        var executed = await next();

        if (!WriteVerbs.Contains(verb)) return;
        if (context.HttpContext.User?.Identity?.IsAuthenticated != true) return;

        var idClaim    = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var emailClaim = context.HttpContext.User.FindFirstValue(ClaimTypes.Email) ?? "";
        if (!Guid.TryParse(idClaim, out var adminId)) return;

        var controller = context.RouteData.Values["controller"]?.ToString() ?? "";
        var action     = context.RouteData.Values["action"]?.ToString() ?? "";
        var entityId   = context.RouteData.Values["id"]?.ToString();
        var ip         = context.HttpContext.Connection.RemoteIpAddress?.ToString();

        var details = JsonSerializer.Serialize(new
        {
            statusCode = context.HttpContext.Response.StatusCode,
            hasException = executed.Exception != null,
            exceptionMessage = executed.Exception?.Message
        });

        await _logger.LogAsync(
            adminUserId: adminId,
            adminEmail:  emailClaim,
            action:      $"{controller}.{action}",
            entityType:  controller,
            entityId:    entityId,
            details:     details,
            ipAddress:   ip);
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Authorization/AuditActionFilter.cs
git commit -m "feat(admin): global audit action filter"
```

---

### Task B5: `LoginThrottleService`

**Files:**
- Create: `HM.AdminPanel/Services/LoginThrottleService.cs`

- [ ] **Step 1: Implementation**

```csharp
// HM.AdminPanel/Services/LoginThrottleService.cs
using Microsoft.Extensions.Caching.Memory;

namespace HM.AdminPanel.Services;

public class LoginThrottleService
{
    private const int MaxAttempts   = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);

    private readonly IMemoryCache _cache;
    public LoginThrottleService(IMemoryCache cache) => _cache = cache;

    public bool IsBlocked(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        return _cache.TryGetValue(Key(ip), out int n) && n >= MaxAttempts;
    }

    public void RecordFailure(string ip)
    {
        if (string.IsNullOrEmpty(ip)) return;
        var n = _cache.TryGetValue(Key(ip), out int v) ? v + 1 : 1;
        _cache.Set(Key(ip), n, Window);
    }

    public void Reset(string ip)
    {
        if (!string.IsNullOrEmpty(ip)) _cache.Remove(Key(ip));
    }

    private static string Key(string ip) => $"admin-login-throttle::{ip}";
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Services/LoginThrottleService.cs
git commit -m "feat(admin): per-IP login throttle service"
```

---

### Task B6: `IDashboardQueryService` + impl (returns real numbers)

**Files:**
- Create: `HM.AdminPanel/ViewModels/Dashboard/DashboardVm.cs`
- Create: `HM.AdminPanel/Services/IDashboardQueryService.cs`
- Create: `HM.AdminPanel/Services/DashboardQueryService.cs`

- [ ] **Step 1: ViewModel**

```csharp
// HM.AdminPanel/ViewModels/Dashboard/DashboardVm.cs
namespace HM.AdminPanel.ViewModels.Dashboard;

public class DashboardVm
{
    public int TotalMerchants     { get; set; }
    public int TotalDrivers       { get; set; }
    public int TotalTruckAccounts { get; set; }
    public int TotalTrucks        { get; set; }
    public int ActiveShipments    { get; set; }
    public int CompletedToday     { get; set; }

    public List<DayCount>    Last30Days       { get; set; } = new();
    public List<StatusCount> ShipmentsByStatus { get; set; } = new();
    public List<RecentItem>  RecentActivity    { get; set; } = new();
}

public record DayCount(DateOnly Day, int Count);
public record StatusCount(string Status, int Count);
public record RecentItem(string Kind, string Title, string Subtitle, DateTime At);
```

- [ ] **Step 2: Interface**

```csharp
// HM.AdminPanel/Services/IDashboardQueryService.cs
using HM.AdminPanel.ViewModels.Dashboard;

namespace HM.AdminPanel.Services;

public interface IDashboardQueryService
{
    Task<DashboardVm> BuildAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Implementation**

```csharp
// HM.AdminPanel/Services/DashboardQueryService.cs
using HM.AdminPanel.ViewModels.Dashboard;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Services;

public class DashboardQueryService : IDashboardQueryService
{
    private readonly ApplicationDbContext _db;
    public DashboardQueryService(ApplicationDbContext db) => _db = db;

    public async Task<DashboardVm> BuildAsync(CancellationToken ct = default)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var since30  = todayUtc.AddDays(-29);

        var vm = new DashboardVm
        {
            TotalMerchants     = await _db.Users.CountAsync(u => u.UserType == UserType.Merchant, ct),
            TotalDrivers       = await _db.Users.CountAsync(u => u.UserType == UserType.Driver, ct),
            TotalTruckAccounts = await _db.Users.CountAsync(u => u.UserType == UserType.TruckAccount, ct),
            TotalTrucks        = await _db.Trucks.CountAsync(ct),
            ActiveShipments    = await _db.Shipments.CountAsync(
                                    s => s.Status != ShipmentStatus.Completed
                                      && s.Status != ShipmentStatus.Cancelled, ct),
            CompletedToday     = await _db.Shipments.CountAsync(
                                    s => s.Status == ShipmentStatus.Completed
                                      && s.CompletedAt >= todayUtc, ct),
        };

        var grouped = await _db.Shipments
            .Where(s => s.StartedAt >= since30)
            .GroupBy(s => s.StartedAt!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        vm.Last30Days = Enumerable.Range(0, 30)
            .Select(i => DateOnly.FromDateTime(since30.AddDays(i)))
            .Select(d => new DayCount(d,
                grouped.FirstOrDefault(x =>
                    DateOnly.FromDateTime(x.Day) == d)?.Count ?? 0))
            .ToList();

        vm.ShipmentsByStatus = await _db.Shipments
            .GroupBy(s => s.Status)
            .Select(g => new StatusCount(g.Key.ToString(), g.Count()))
            .ToListAsync(ct);

        var recentShipments = await _db.Shipments
            .OrderByDescending(s => s.StartedAt)
            .Take(10)
            .Select(s => new RecentItem(
                "Shipment",
                $"Shipment {s.Id}",
                s.Status.ToString(),
                s.StartedAt ?? DateTime.UtcNow))
            .ToListAsync(ct);
        vm.RecentActivity = recentShipments;

        return vm;
    }
}
```

- [ ] **Step 4: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Services/IDashboardQueryService.cs HM.AdminPanel/Services/DashboardQueryService.cs HM.AdminPanel/ViewModels/Dashboard/DashboardVm.cs
git commit -m "feat(admin): dashboard query service + DashboardVm"
```

---

### Task B7: DI extension + `Program.cs` wiring

**Files:**
- Create: `HM.AdminPanel/Extensions/ServiceCollectionExtensions.cs`
- Create: `HM.AdminPanel/Extensions/HttpContextExtensions.cs`
- Replace: `HM.AdminPanel/Program.cs`

- [ ] **Step 1: `HttpContextExtensions.cs`**

```csharp
// HM.AdminPanel/Extensions/HttpContextExtensions.cs
using System.Security.Claims;

namespace HM.AdminPanel.Extensions;

public static class HttpContextExtensions
{
    public static Guid? CurrentAdminId(this HttpContext ctx)
    {
        var raw = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    public static string CurrentAdminEmail(this HttpContext ctx) =>
        ctx.User.FindFirstValue(ClaimTypes.Email) ?? "";
}
```

- [ ] **Step 2: `ServiceCollectionExtensions.cs`**

```csharp
// HM.AdminPanel/Extensions/ServiceCollectionExtensions.cs
using HM.AdminPanel.Authorization;
using HM.AdminPanel.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace HM.AdminPanel.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAdminAuth(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(o =>
            {
                o.LoginPath        = "/Account/Login";
                o.LogoutPath       = "/Account/Logout";
                o.AccessDeniedPath = "/Account/AccessDenied";
                o.ExpireTimeSpan   = TimeSpan.FromHours(8);
                o.SlidingExpiration = true;
                o.Cookie.Name       = "HM.Admin";
                o.Cookie.HttpOnly   = true;
                o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                o.Cookie.SameSite   = SameSiteMode.Lax;
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(AdminPolicies.RequireAdmin,
                p => p.RequireRole(AdminRoles.SuperAdmin, AdminRoles.Support, AdminRoles.ReadOnly));
            options.AddPolicy(AdminPolicies.RequireSuperAdmin,
                p => p.RequireRole(AdminRoles.SuperAdmin));
            options.AddPolicy(AdminPolicies.RequireWriteAccess,
                p => p.RequireRole(AdminRoles.SuperAdmin, AdminRoles.Support));

            options.FallbackPolicy = options.GetPolicy(AdminPolicies.RequireAdmin);
        });

        return services;
    }

    public static IServiceCollection AddAdminServices(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddSingleton<LoginThrottleService>();
        services.AddScoped<IAdminAuditLogger, AdminAuditLogger>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<AuditActionFilter>();
        return services;
    }
}
```

- [ ] **Step 3: Replace `Program.cs`**

```csharp
// HM.AdminPanel/Program.cs
using HM.AdminPanel.Authorization;
using HM.AdminPanel.Extensions;
using HM.Infrastructure;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAdminAuth();
builder.Services.AddAdminServices();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<AuditActionFilter>();
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error/500");
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/Error/{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["Cache-Control"] = "no-store";
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();
```

- [ ] **Step 4: Build**

Run: `dotnet build HM.AdminPanel/HM.AdminPanel.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/Extensions/ HM.AdminPanel/Program.cs
git commit -m "feat(admin): wire cookie auth, policies, audit filter in Program.cs"
```

---

## Phase C — Static assets, layout, shared views

### Task C1: Vendor AdminLTE 3 + libs into `wwwroot`

**Files:**
- Create: `HM.AdminPanel/wwwroot/adminlte/css/adminlte.min.css`
- Create: `HM.AdminPanel/wwwroot/adminlte/js/adminlte.min.js`
- Create: `HM.AdminPanel/wwwroot/lib/jquery/jquery.min.js`
- Create: `HM.AdminPanel/wwwroot/lib/bootstrap/css/bootstrap.min.css`
- Create: `HM.AdminPanel/wwwroot/lib/bootstrap/js/bootstrap.bundle.min.js`
- Create: `HM.AdminPanel/wwwroot/lib/fontawesome/css/all.min.css`
- Create: `HM.AdminPanel/wwwroot/lib/datatables/datatables.min.css`
- Create: `HM.AdminPanel/wwwroot/lib/datatables/datatables.min.js`
- Create: `HM.AdminPanel/wwwroot/lib/chart.js/chart.min.js`
- Create: `HM.AdminPanel/wwwroot/lib/sweetalert2/sweetalert2.min.css`
- Create: `HM.AdminPanel/wwwroot/lib/sweetalert2/sweetalert2.min.js`
- Create: `HM.AdminPanel/wwwroot/leaflet/leaflet.css`
- Create: `HM.AdminPanel/wwwroot/leaflet/leaflet.js`
- Create: `HM.AdminPanel/wwwroot/signalr/signalr.min.js`

- [ ] **Step 1: Download AdminLTE 3 release** — from https://github.com/ColorlibHQ/AdminLTE/releases (use v3.2.0). Unzip and copy `dist/css/adminlte.min.css` and `dist/js/adminlte.min.js` into `HM.AdminPanel/wwwroot/adminlte/`. Also copy `plugins/fontawesome-free/` into `HM.AdminPanel/wwwroot/lib/fontawesome/`.

- [ ] **Step 2: jQuery 3.7** — download `jquery.min.js` from https://code.jquery.com/jquery-3.7.1.min.js into `wwwroot/lib/jquery/`.

- [ ] **Step 3: Bootstrap 5** — download `bootstrap.min.css` + `bootstrap.bundle.min.js` from https://getbootstrap.com/docs/5.3/getting-started/download/ into `wwwroot/lib/bootstrap/`.

- [ ] **Step 4: DataTables** — generate a bundle at https://datatables.net/download/ (DataTables + Bootstrap 5 integration) and place the resulting `datatables.min.css` and `datatables.min.js` into `wwwroot/lib/datatables/`.

- [ ] **Step 5: Chart.js v4** — download `chart.umd.min.js` from https://cdn.jsdelivr.net/npm/chart.js@4 and rename to `chart.min.js` into `wwwroot/lib/chart.js/`.

- [ ] **Step 6: SweetAlert2** — download `sweetalert2.min.css` + `sweetalert2.min.js` from https://cdn.jsdelivr.net/npm/sweetalert2@11/dist/ into `wwwroot/lib/sweetalert2/`.

- [ ] **Step 7: Leaflet** — download `leaflet.css` + `leaflet.js` from https://leafletjs.com/download.html into `wwwroot/leaflet/`.

- [ ] **Step 8: SignalR JS client** — download `signalr.min.js` from https://cdn.jsdelivr.net/npm/@microsoft/signalr@8/dist/browser/signalr.min.js into `wwwroot/signalr/`.

- [ ] **Step 9: Verify file presence**

Run: `ls HM.AdminPanel/wwwroot/adminlte/css/adminlte.min.css`
Expected: File exists, non-zero size.

- [ ] **Step 10: Commit (large vendor blob commit)**

```bash
git add HM.AdminPanel/wwwroot/
git commit -m "chore(admin): vendor AdminLTE 3, jQuery, Bootstrap 5, DataTables, Chart.js, Leaflet, SignalR client"
```

---

### Task C2: `_ViewImports`, `_ViewStart`, `site.css`, `site.js`

**Files:**
- Replace: `HM.AdminPanel/Views/_ViewImports.cshtml`
- Replace: `HM.AdminPanel/Views/_ViewStart.cshtml`
- Create: `HM.AdminPanel/wwwroot/css/site.css`
- Create: `HM.AdminPanel/wwwroot/js/site.js`

- [ ] **Step 1: `_ViewImports.cshtml`**

```cshtml
@using HM.AdminPanel
@using HM.AdminPanel.ViewModels
@using HM.AdminPanel.Authorization
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

- [ ] **Step 2: `_ViewStart.cshtml`**

```cshtml
@{
    Layout = "_Layout";
}
```

- [ ] **Step 3: `site.css`** — minimal overrides:

```css
.sidebar-brand-text { font-weight: 600; }
.small-box .icon { opacity: 0.6; }
table.dataTable thead th { white-space: nowrap; }
```

- [ ] **Step 4: `site.js`** — flash + confirm helpers:

```js
// HM.AdminPanel/wwwroot/js/site.js
(function () {
    document.addEventListener('DOMContentLoaded', function () {
        var s = document.getElementById('flash-success')?.innerText;
        var e = document.getElementById('flash-error')?.innerText;
        if (s && window.Swal) Swal.fire({ icon: 'success', title: s, timer: 2500, showConfirmButton: false });
        if (e && window.Swal) Swal.fire({ icon: 'error', title: e });

        document.querySelectorAll('form[data-confirm]').forEach(function (form) {
            form.addEventListener('submit', function (ev) {
                ev.preventDefault();
                Swal.fire({
                    title: form.getAttribute('data-confirm'),
                    icon: 'warning',
                    showCancelButton: true,
                    confirmButtonText: 'Yes',
                }).then(function (r) { if (r.isConfirmed) form.submit(); });
            });
        });
    });
})();
```

- [ ] **Step 5: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Views/_ViewImports.cshtml HM.AdminPanel/Views/_ViewStart.cshtml HM.AdminPanel/wwwroot/css/ HM.AdminPanel/wwwroot/js/
git commit -m "feat(admin): view imports, site css/js (flash + confirm)"
```

---

### Task C3: `_LoginLayout.cshtml`

**Files:**
- Create: `HM.AdminPanel/Views/Shared/_LoginLayout.cshtml`

- [ ] **Step 1: Create**

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <title>HM Admin · @ViewData["Title"]</title>
    <link rel="stylesheet" href="~/lib/fontawesome/css/all.min.css" />
    <link rel="stylesheet" href="~/lib/bootstrap/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/adminlte/css/adminlte.min.css" />
</head>
<body class="hold-transition login-page">
    <div class="login-box">
        <div class="login-logo"><b>HM</b> Admin</div>
        @RenderBody()
    </div>
    <script src="~/lib/jquery/jquery.min.js"></script>
    <script src="~/lib/bootstrap/js/bootstrap.bundle.min.js"></script>
    <script src="~/adminlte/js/adminlte.min.js"></script>
</body>
</html>
```

- [ ] **Step 2: Commit**

```bash
git add HM.AdminPanel/Views/Shared/_LoginLayout.cshtml
git commit -m "feat(admin): login layout"
```

---

### Task C4: `_Sidebar.cshtml` (role-aware menu)

**Files:**
- Create: `HM.AdminPanel/Views/Shared/_Sidebar.cshtml`

- [ ] **Step 1: Create the partial**

```cshtml
@using HM.AdminPanel.Authorization
@{
    var canWrite = User.IsInRole(AdminRoles.SuperAdmin) || User.IsInRole(AdminRoles.Support);
    var isSuper  = User.IsInRole(AdminRoles.SuperAdmin);
}
<aside class="main-sidebar sidebar-dark-primary elevation-4">
    <a href="/" class="brand-link">
        <span class="brand-text font-weight-light">HM Admin</span>
    </a>
    <div class="sidebar">
        <nav class="mt-2">
            <ul class="nav nav-pills nav-sidebar flex-column" data-widget="treeview" role="menu">
                <li class="nav-item">
                    <a href="/Dashboard" class="nav-link"><i class="nav-icon fas fa-tachometer-alt"></i><p>Dashboard</p></a>
                </li>
                <li class="nav-item">
                    <a href="/Dashboard/LiveMap" class="nav-link"><i class="nav-icon fas fa-map"></i><p>Live Map</p></a>
                </li>
                <li class="nav-header">USERS</li>
                <li class="nav-item"><a href="/Merchants" class="nav-link"><i class="nav-icon fas fa-store"></i><p>Merchants</p></a></li>
                <li class="nav-item"><a href="/Drivers" class="nav-link"><i class="nav-icon fas fa-id-card"></i><p>Drivers</p></a></li>
                <li class="nav-item"><a href="/TruckAccounts" class="nav-link"><i class="nav-icon fas fa-users-cog"></i><p>Truck Accounts</p></a></li>
                <li class="nav-header">OPERATIONS</li>
                <li class="nav-item"><a href="/Trucks" class="nav-link"><i class="nav-icon fas fa-truck"></i><p>Trucks</p></a></li>
                <li class="nav-item"><a href="/Shipments" class="nav-link"><i class="nav-icon fas fa-box"></i><p>Shipments</p></a></li>
                <li class="nav-item"><a href="/ShipmentRequests" class="nav-link"><i class="nav-icon fas fa-clipboard-list"></i><p>Shipment Requests</p></a></li>
            </ul>
        </nav>
    </div>
</aside>
```

- [ ] **Step 2: Commit**

```bash
git add HM.AdminPanel/Views/Shared/_Sidebar.cshtml
git commit -m "feat(admin): role-aware sidebar partial"
```

---

### Task C5: `_Layout.cshtml`

**Files:**
- Create/Replace: `HM.AdminPanel/Views/Shared/_Layout.cshtml`

- [ ] **Step 1: Create**

```cshtml
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width,initial-scale=1" />
    <title>HM Admin · @ViewData["Title"]</title>
    <link rel="stylesheet" href="~/lib/fontawesome/css/all.min.css" />
    <link rel="stylesheet" href="~/lib/bootstrap/css/bootstrap.min.css" />
    <link rel="stylesheet" href="~/adminlte/css/adminlte.min.css" />
    <link rel="stylesheet" href="~/lib/datatables/datatables.min.css" />
    <link rel="stylesheet" href="~/lib/sweetalert2/sweetalert2.min.css" />
    <link rel="stylesheet" href="~/css/site.css" />
    @await RenderSectionAsync("Head", required: false)
</head>
<body class="hold-transition sidebar-mini layout-fixed">
<div class="wrapper">
    <nav class="main-header navbar navbar-expand navbar-white navbar-light">
        <ul class="navbar-nav">
            <li class="nav-item"><a class="nav-link" data-widget="pushmenu" href="#"><i class="fas fa-bars"></i></a></li>
        </ul>
        <ul class="navbar-nav ml-auto">
            <li class="nav-item">
                <span class="nav-link">@User.Identity?.Name</span>
            </li>
            <li class="nav-item">
                <form method="post" action="/Account/Logout" class="form-inline">
                    @Html.AntiForgeryToken()
                    <button type="submit" class="btn btn-link nav-link">Logout</button>
                </form>
            </li>
        </ul>
    </nav>

    @await Html.PartialAsync("_Sidebar")

    <div class="content-wrapper">
        <section class="content-header">
            <h1>@ViewData["Title"]</h1>
        </section>
        <section class="content">
            <div class="container-fluid">
                @if (TempData["Success"] is string s)
                {
                    <div id="flash-success" style="display:none">@s</div>
                }
                @if (TempData["Error"] is string e)
                {
                    <div id="flash-error" style="display:none">@e</div>
                }
                @RenderBody()
            </div>
        </section>
    </div>

    <footer class="main-footer">
        <strong>HM Admin</strong> &copy; @DateTime.UtcNow.Year
    </footer>
</div>

<script src="~/lib/jquery/jquery.min.js"></script>
<script src="~/lib/bootstrap/js/bootstrap.bundle.min.js"></script>
<script src="~/adminlte/js/adminlte.min.js"></script>
<script src="~/lib/datatables/datatables.min.js"></script>
<script src="~/lib/sweetalert2/sweetalert2.min.js"></script>
<script src="~/lib/chart.js/chart.min.js"></script>
<script src="~/js/site.js"></script>
@await RenderSectionAsync("Scripts", required: false)
</body>
</html>
```

- [ ] **Step 2: Commit**

```bash
git add HM.AdminPanel/Views/Shared/_Layout.cshtml
git commit -m "feat(admin): main layout with AdminLTE shell"
```

---

### Task C6: `_Pager.cshtml` partial + `PagedResult<T>` VM

**Files:**
- Create: `HM.AdminPanel/ViewModels/Common/PagedResult.cs`
- Create: `HM.AdminPanel/Views/Shared/_Pager.cshtml`

- [ ] **Step 1: `PagedResult<T>`**

```csharp
// HM.AdminPanel/ViewModels/Common/PagedResult.cs
namespace HM.AdminPanel.ViewModels.Common;

public class PagedResult<T>
{
    public List<T> Items     { get; set; } = new();
    public int     Page      { get; set; } = 1;
    public int     PageSize  { get; set; } = 25;
    public int     Total     { get; set; }
    public int     TotalPages => (int)Math.Ceiling(Total / (double)PageSize);
}
```

- [ ] **Step 2: `_Pager.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Common.PagedResult<object>
@{
    var page  = Model.Page;
    var total = Model.TotalPages;
    string Url(int p) => Context.Request.Path + "?" +
        string.Join("&",
            Context.Request.Query
                .Where(kv => kv.Key != "page")
                .Select(kv => $"{kv.Key}={kv.Value}")
                .Append($"page={p}"));
}
<nav>
  <ul class="pagination">
    <li class="page-item @(page <= 1 ? "disabled" : "")">
      <a class="page-link" href="@Url(page - 1)">Prev</a>
    </li>
    <li class="page-item disabled"><span class="page-link">@page / @total</span></li>
    <li class="page-item @(page >= total ? "disabled" : "")">
      <a class="page-link" href="@Url(page + 1)">Next</a>
    </li>
  </ul>
</nav>
```

- [ ] **Step 3: Commit**

```bash
git add HM.AdminPanel/ViewModels/Common/ HM.AdminPanel/Views/Shared/_Pager.cshtml
git commit -m "feat(admin): PagedResult VM + pager partial"
```

---

### Task C7: Error pages + `ErrorController`

**Files:**
- Create: `HM.AdminPanel/Controllers/ErrorController.cs`
- Create: `HM.AdminPanel/Views/Error/NotFound.cshtml`
- Create: `HM.AdminPanel/Views/Error/AccessDenied.cshtml`
- Create: `HM.AdminPanel/Views/Error/GenericError.cshtml`

- [ ] **Step 1: `ErrorController`**

```csharp
// HM.AdminPanel/Controllers/ErrorController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HM.AdminPanel.Controllers;

[AllowAnonymous]
[Route("Error")]
public class ErrorController : Controller
{
    [HttpGet("{code:int}")]
    public IActionResult Status(int code)
    {
        return code switch
        {
            404 => View("NotFound"),
            403 => View("AccessDenied"),
            _   => View("GenericError")
        };
    }
}
```

- [ ] **Step 2: `NotFound.cshtml`**

```cshtml
@{ ViewData["Title"] = "Not Found"; Layout = "_Layout"; }
<div class="error-page">
    <h2 class="headline text-warning">404</h2>
    <div class="error-content">
        <h3>Page not found.</h3>
        <p><a href="/">Back to dashboard</a></p>
    </div>
</div>
```

- [ ] **Step 3: `AccessDenied.cshtml`**

```cshtml
@{ ViewData["Title"] = "Access denied"; Layout = "_Layout"; }
<div class="error-page">
    <h2 class="headline text-danger">403</h2>
    <div class="error-content">
        <h3>You don't have permission to view this page.</h3>
        <p><a href="/">Back to dashboard</a></p>
    </div>
</div>
```

- [ ] **Step 4: `GenericError.cshtml`**

```cshtml
@{ ViewData["Title"] = "Error"; Layout = "_Layout"; }
<div class="error-page">
    <h2 class="headline text-danger">500</h2>
    <div class="error-content">
        <h3>Something went wrong.</h3>
        <p><a href="/">Back to dashboard</a></p>
    </div>
</div>
```

- [ ] **Step 5: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Controllers/ErrorController.cs HM.AdminPanel/Views/Error/
git commit -m "feat(admin): error pages (404/403/500)"
```

---

## Phase D — Module 11: Admin Authentication

### Task D1: `LoginVm`

**Files:**
- Create: `HM.AdminPanel/ViewModels/Account/LoginVm.cs`

- [ ] **Step 1: Create**

```csharp
// HM.AdminPanel/ViewModels/Account/LoginVm.cs
using System.ComponentModel.DataAnnotations;

namespace HM.AdminPanel.ViewModels.Account;

public class LoginVm
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
```

- [ ] **Step 2: Commit**

```bash
git add HM.AdminPanel/ViewModels/Account/LoginVm.cs
git commit -m "feat(admin): LoginVm"
```

---

### Task D2: `AccountController` (GET Login)

**Files:**
- Create: `HM.AdminPanel/Controllers/AccountController.cs`
- Create: `HM.AdminPanel/Views/Account/Login.cshtml`

- [ ] **Step 1: Controller skeleton**

```csharp
// HM.AdminPanel/Controllers/AccountController.cs
using System.Security.Claims;
using HM.AdminPanel.Authorization;
using HM.AdminPanel.Services;
using HM.AdminPanel.ViewModels.Account;
using HM.Domain.Entities;
using HM.Infrastructure.Data;
using HM.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser>  _userMgr;
    private readonly SignInManager<ApplicationUser> _signInMgr;
    private readonly ApplicationDbContext           _db;
    private readonly LoginThrottleService           _throttle;

    public AccountController(
        UserManager<ApplicationUser> userMgr,
        SignInManager<ApplicationUser> signInMgr,
        ApplicationDbContext db,
        LoginThrottleService throttle)
    {
        _userMgr   = userMgr;
        _signInMgr = signInMgr;
        _db        = db;
        _throttle  = throttle;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["Title"] = "Sign in";
        return View(new LoginVm { ReturnUrl = returnUrl });
    }
}
```

- [ ] **Step 2: `Login.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Account.LoginVm
@{ Layout = "_LoginLayout"; ViewData["Title"] = "Sign in"; }
<div class="card">
  <div class="card-body login-card-body">
    <p class="login-box-msg">Sign in to start your session</p>
    <form method="post" asp-action="Login" asp-controller="Account">
        @Html.AntiForgeryToken()
        <input type="hidden" asp-for="ReturnUrl" />
        <div class="input-group mb-3">
            <input asp-for="Email" class="form-control" placeholder="Email" />
            <div class="input-group-append"><div class="input-group-text"><span class="fas fa-envelope"></span></div></div>
        </div>
        <span asp-validation-for="Email" class="text-danger"></span>
        <div class="input-group mb-3">
            <input asp-for="Password" class="form-control" placeholder="Password" />
            <div class="input-group-append"><div class="input-group-text"><span class="fas fa-lock"></span></div></div>
        </div>
        <span asp-validation-for="Password" class="text-danger"></span>
        <div class="row">
            <div class="col-8">
                <div class="icheck-primary">
                    <input asp-for="RememberMe" type="checkbox" id="remember" />
                    <label for="remember">Remember Me</label>
                </div>
            </div>
            <div class="col-4">
                <button type="submit" class="btn btn-primary btn-block">Sign In</button>
            </div>
        </div>
        @if (TempData["Error"] is string err) {
            <div class="alert alert-danger mt-3 mb-0">@err</div>
        }
    </form>
  </div>
</div>
```

- [ ] **Step 3: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Controllers/AccountController.cs HM.AdminPanel/Views/Account/Login.cshtml
git commit -m "feat(admin): Login GET + view"
```

---

### Task D3: POST Login + Logout + AccessDenied

**Files:**
- Modify: `HM.AdminPanel/Controllers/AccountController.cs`

- [ ] **Step 1: Append actions to `AccountController`**

```csharp
[HttpPost, ValidateAntiForgeryToken]
public async Task<IActionResult> Login(LoginVm vm)
{
    var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
    var ua = Request.Headers.UserAgent.ToString();

    if (_throttle.IsBlocked(ip))
    {
        TempData["Error"] = "Too many failed attempts. Try again later.";
        return View(vm);
    }

    if (!ModelState.IsValid) return View(vm);

    async Task RecordAttempt(bool success)
    {
        _db.AdminLoginAttempts.Add(new AdminLoginAttempt
        {
            Id = Guid.NewGuid(),
            Email = vm.Email,
            Success = success,
            IpAddress = ip,
            UserAgent = ua,
            CreatedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
    }

    var appUser = await _userMgr.FindByEmailAsync(vm.Email);
    if (appUser is null)
    {
        _throttle.RecordFailure(ip);
        await RecordAttempt(false);
        TempData["Error"] = "Invalid credentials.";
        return View(vm);
    }

    // Block check on Domain User (shares Guid with ApplicationUser)
    var domain = await _db.Users.FirstOrDefaultAsync(u => u.Id == appUser.Id);
    if (domain is { IsBlocked: true })
    {
        await RecordAttempt(false);
        TempData["Error"] = "Account is blocked.";
        return View(vm);
    }

    var isAdmin = await _userMgr.IsInRoleAsync(appUser, AdminRoles.Admin);
    if (!isAdmin)
    {
        _throttle.RecordFailure(ip);
        await RecordAttempt(false);
        TempData["Error"] = "Account is not an admin.";
        return View(vm);
    }

    var check = await _signInMgr.CheckPasswordSignInAsync(appUser, vm.Password, lockoutOnFailure: false);
    if (!check.Succeeded)
    {
        _throttle.RecordFailure(ip);
        await RecordAttempt(false);
        TempData["Error"] = "Invalid credentials.";
        return View(vm);
    }

    var roles = await _userMgr.GetRolesAsync(appUser);
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, appUser.Id.ToString()),
        new(ClaimTypes.Name,           appUser.Email ?? appUser.UserName ?? ""),
        new(ClaimTypes.Email,          appUser.Email ?? "")
    };
    foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

    var identity  = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
    var principal = new ClaimsPrincipal(identity);

    await HttpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        principal,
        new AuthenticationProperties { IsPersistent = vm.RememberMe });

    _throttle.Reset(ip);
    await RecordAttempt(true);

    if (!string.IsNullOrEmpty(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
        return Redirect(vm.ReturnUrl);

    return RedirectToAction("Index", "Dashboard");
}

[HttpPost, ValidateAntiForgeryToken, Authorize]
public async Task<IActionResult> Logout()
{
    await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return RedirectToAction(nameof(Login));
}

[HttpGet]
public IActionResult AccessDenied() => View("~/Views/Error/AccessDenied.cshtml");
```

- [ ] **Step 2: Build**

Run: `dotnet build HM.AdminPanel/HM.AdminPanel.csproj`
Expected: Build succeeded.

- [ ] **Step 3: Smoke test**

```bash
dotnet run --project HM.AdminPanel
```
- Open `https://localhost:7050/` → redirects to `/Account/Login`.
- Login with seeded admin credentials from `appsettings.json` → redirects to `/Dashboard` (will 500 because controller doesn't exist yet — expected at this step; cookie should be set).

- [ ] **Step 4: Commit**

```bash
git add HM.AdminPanel/Controllers/AccountController.cs
git commit -m "feat(admin): Login POST, Logout, AccessDenied"
```

---

## Phase E — Module 1: Dashboard

### Task E1: `DashboardController` + Index view (KPIs + charts)

**Files:**
- Create: `HM.AdminPanel/Controllers/DashboardController.cs`
- Create: `HM.AdminPanel/Views/Dashboard/Index.cshtml`

- [ ] **Step 1: Controller**

```csharp
// HM.AdminPanel/Controllers/DashboardController.cs
using HM.AdminPanel.Authorization;
using HM.AdminPanel.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class DashboardController : Controller
{
    private readonly IDashboardQueryService _query;
    public DashboardController(IDashboardQueryService query) => _query = query;

    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Dashboard";
        var vm = await _query.BuildAsync();
        return View(vm);
    }
}
```

- [ ] **Step 2: `Index.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Dashboard.DashboardVm

<div class="row">
    <div class="col-lg-3 col-6">
        <div class="small-box bg-info">
            <div class="inner"><h3>@Model.TotalMerchants</h3><p>Merchants</p></div>
            <div class="icon"><i class="fas fa-store"></i></div>
        </div>
    </div>
    <div class="col-lg-3 col-6">
        <div class="small-box bg-success">
            <div class="inner"><h3>@Model.TotalDrivers</h3><p>Drivers</p></div>
            <div class="icon"><i class="fas fa-id-card"></i></div>
        </div>
    </div>
    <div class="col-lg-3 col-6">
        <div class="small-box bg-warning">
            <div class="inner"><h3>@Model.TotalTrucks</h3><p>Trucks</p></div>
            <div class="icon"><i class="fas fa-truck"></i></div>
        </div>
    </div>
    <div class="col-lg-3 col-6">
        <div class="small-box bg-danger">
            <div class="inner"><h3>@Model.ActiveShipments</h3><p>Active shipments</p></div>
            <div class="icon"><i class="fas fa-box"></i></div>
        </div>
    </div>
</div>

<div class="row">
    <div class="col-md-8">
        <div class="card"><div class="card-header"><h3 class="card-title">Shipments — last 30 days</h3></div>
            <div class="card-body"><canvas id="chartDaily" height="120"></canvas></div></div>
    </div>
    <div class="col-md-4">
        <div class="card"><div class="card-header"><h3 class="card-title">By status</h3></div>
            <div class="card-body"><canvas id="chartStatus" height="200"></canvas></div></div>
    </div>
</div>

<div class="card">
    <div class="card-header"><h3 class="card-title">Recent activity</h3></div>
    <div class="card-body p-0">
        <table class="table table-striped">
            <thead><tr><th>When</th><th>Kind</th><th>Title</th><th>Detail</th></tr></thead>
            <tbody>
            @foreach (var r in Model.RecentActivity)
            {
                <tr><td>@r.At.ToString("u")</td><td>@r.Kind</td><td>@r.Title</td><td>@r.Subtitle</td></tr>
            }
            </tbody>
        </table>
    </div>
</div>

@section Scripts {
<script>
    const daily = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.Last30Days));
    const byStatus = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.ShipmentsByStatus));

    new Chart(document.getElementById('chartDaily').getContext('2d'), {
        type: 'line',
        data: {
            labels: daily.map(d => d.day),
            datasets: [{ label: 'Shipments', data: daily.map(d => d.count), borderWidth: 2 }]
        }
    });

    new Chart(document.getElementById('chartStatus').getContext('2d'), {
        type: 'doughnut',
        data: {
            labels: byStatus.map(s => s.status),
            datasets: [{ data: byStatus.map(s => s.count) }]
        }
    });
</script>
}
```

- [ ] **Step 3: Build + run + smoke test**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
dotnet run --project HM.AdminPanel
```
- Sign in → dashboard renders with KPI cards, line chart, doughnut chart, recent table.

- [ ] **Step 4: Commit**

```bash
git add HM.AdminPanel/Controllers/DashboardController.cs HM.AdminPanel/Views/Dashboard/Index.cshtml
git commit -m "feat(admin): dashboard index with KPIs and charts"
```

---

### Task E2: Live Map page (Leaflet + SignalR)

**Files:**
- Create: `HM.AdminPanel/ViewModels/Dashboard/LiveMapVm.cs`
- Modify: `HM.AdminPanel/Controllers/DashboardController.cs`
- Create: `HM.AdminPanel/Views/Dashboard/LiveMap.cshtml`

- [ ] **Step 1: `LiveMapVm`**

```csharp
// HM.AdminPanel/ViewModels/Dashboard/LiveMapVm.cs
namespace HM.AdminPanel.ViewModels.Dashboard;

public class LiveMapVm
{
    public string  HubUrl   { get; set; } = string.Empty;
    public List<ActiveShipmentPin> Pins { get; set; } = new();
}

public record ActiveShipmentPin(Guid ShipmentId, double? Lat, double? Lng, string Status);
```

- [ ] **Step 2: Add `LiveMap` action** to `DashboardController` (above existing `Index`):

```csharp
public async Task<IActionResult> LiveMap([FromServices] HM.Infrastructure.Data.ApplicationDbContext db,
                                         [FromServices] Microsoft.Extensions.Configuration.IConfiguration cfg)
{
    ViewData["Title"] = "Live Map";
    var pins = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ToListAsync(
        db.Shipments
          .Where(s => s.Status != HM.Domain.Enums.ShipmentStatus.Completed
                   && s.Status != HM.Domain.Enums.ShipmentStatus.Cancelled)
          .Select(s => new HM.AdminPanel.ViewModels.Dashboard.ActiveShipmentPin(
              s.Id, s.CurrentLat, s.CurrentLng, s.Status.ToString())));

    return View(new HM.AdminPanel.ViewModels.Dashboard.LiveMapVm
    {
        HubUrl = cfg["AdminPanel:SignalRHubUrl"] ?? "",
        Pins   = pins
    });
}
```

- [ ] **Step 3: `LiveMap.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Dashboard.LiveMapVm

@section Head {
    <link rel="stylesheet" href="~/leaflet/leaflet.css" />
}

<div id="map" style="height:600px;"></div>

@section Scripts {
    <script src="~/leaflet/leaflet.js"></script>
    <script src="~/signalr/signalr.min.js"></script>
    <script>
        const initial = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.Pins));
        const map = L.map('map').setView([30.05, 31.25], 6);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png',
            { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(map);

        const markers = new Map();
        function upsert(id, lat, lng, status) {
            if (lat == null || lng == null) return;
            if (markers.has(id)) {
                markers.get(id).setLatLng([lat, lng]).setPopupContent(`${id}<br>${status}`);
            } else {
                markers.set(id, L.marker([lat, lng]).addTo(map).bindPopup(`${id}<br>${status}`));
            }
        }
        initial.forEach(p => upsert(p.shipmentId, p.lat, p.lng, p.status));

        const hubUrl = @Html.Raw(System.Text.Json.JsonSerializer.Serialize(Model.HubUrl));
        if (hubUrl) {
            const conn = new signalR.HubConnectionBuilder().withUrl(hubUrl).withAutomaticReconnect().build();
            conn.on('LocationUpdated', function (payload) {
                upsert(payload.shipmentId, payload.lat, payload.lng, payload.status || 'InProgress');
            });
            conn.start().catch(err => console.error(err));
        }
    </script>
}
```

- [ ] **Step 4: Build + smoke test**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
dotnet run --project HM.AdminPanel
```
- Navigate to `/Dashboard/LiveMap` → map renders, markers visible for active shipments. Console clear of errors (SignalR connection only succeeds if the WebApi is also running locally).

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/ViewModels/Dashboard/LiveMapVm.cs HM.AdminPanel/Controllers/DashboardController.cs HM.AdminPanel/Views/Dashboard/LiveMap.cshtml
git commit -m "feat(admin): live shipment map (Leaflet + SignalR client)"
```

---

## Phase F — Module 2: Users (Merchants, Drivers, TruckAccounts)

> **Shared pattern note.** Each user-type controller follows the same shape: paginated Index with a filter VM, Details page, and three POST actions (Block / Unblock / Verify). To keep the plan readable, Merchants gets the fully detailed write-up below (F1–F3). Drivers (F4) and TruckAccounts (F5) reuse the same shape with the entity names swapped — full code in those tasks too, but condensed.

### Task F1: Merchants ViewModels

**Files:**
- Create: `HM.AdminPanel/ViewModels/Merchants/MerchantFilterVm.cs`
- Create: `HM.AdminPanel/ViewModels/Merchants/MerchantListItemVm.cs`
- Create: `HM.AdminPanel/ViewModels/Merchants/MerchantListVm.cs`
- Create: `HM.AdminPanel/ViewModels/Merchants/MerchantDetailVm.cs`

- [ ] **Step 1: `MerchantFilterVm`**

```csharp
namespace HM.AdminPanel.ViewModels.Merchants;
public class MerchantFilterVm
{
    public string? Search   { get; set; }
    public bool?   IsBlocked{ get; set; }
    public bool?   IsActive { get; set; }
    public int     Page     { get; set; } = 1;
    public int     PageSize { get; set; } = 25;
}
```

- [ ] **Step 2: `MerchantListItemVm`**

```csharp
namespace HM.AdminPanel.ViewModels.Merchants;
public class MerchantListItemVm
{
    public Guid   Id          { get; set; }
    public string FullName    { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Email       { get; set; } = "";
    public bool   IsActive    { get; set; }
    public bool   IsBlocked   { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

- [ ] **Step 3: `MerchantListVm`**

```csharp
using HM.AdminPanel.ViewModels.Common;
namespace HM.AdminPanel.ViewModels.Merchants;
public class MerchantListVm
{
    public MerchantFilterVm Filter { get; set; } = new();
    public PagedResult<MerchantListItemVm> Result { get; set; } = new();
}
```

- [ ] **Step 4: `MerchantDetailVm`**

```csharp
namespace HM.AdminPanel.ViewModels.Merchants;
public class MerchantDetailVm
{
    public Guid     Id            { get; set; }
    public string   FullName      { get; set; } = "";
    public string   PhoneNumber   { get; set; } = "";
    public string   Email         { get; set; } = "";
    public bool     IsActive      { get; set; }
    public bool     IsBlocked     { get; set; }
    public DateTime? BlockedAt    { get; set; }
    public string?  BlockedReason { get; set; }
    public DateTime CreatedAt     { get; set; }
    public int      ShipmentCount { get; set; }
}
```

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/ViewModels/Merchants/
git commit -m "feat(admin): Merchants VMs"
```

---

### Task F2: `MerchantsController` (Index + Details)

**Files:**
- Create: `HM.AdminPanel/Controllers/MerchantsController.cs`

- [ ] **Step 1: Controller**

```csharp
// HM.AdminPanel/Controllers/MerchantsController.cs
using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.Common;
using HM.AdminPanel.ViewModels.Merchants;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class MerchantsController : Controller
{
    private readonly ApplicationDbContext _db;
    public MerchantsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MerchantFilterVm filter)
    {
        ViewData["Title"] = "Merchants";

        var q = _db.Users.Where(u => u.UserType == UserType.Merchant);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(u => u.FullName.Contains(s)
                          || u.PhoneNumber.Contains(s)
                          || u.Email.Contains(s));
        }
        if (filter.IsBlocked.HasValue) q = q.Where(u => u.IsBlocked == filter.IsBlocked);
        if (filter.IsActive.HasValue)  q = q.Where(u => u.IsActive  == filter.IsActive);

        var total = await q.CountAsync();
        var items = await q.OrderByDescending(u => u.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(u => new MerchantListItemVm
            {
                Id = u.Id, FullName = u.FullName, PhoneNumber = u.PhoneNumber,
                Email = u.Email, IsActive = u.IsActive, IsBlocked = u.IsBlocked,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return View(new MerchantListVm
        {
            Filter = filter,
            Result = new PagedResult<MerchantListItemVm>
            {
                Items = items, Page = filter.Page, PageSize = filter.PageSize, Total = total
            }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Merchant";
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Merchant);
        if (u is null) return NotFound();

        var count = await _db.ShipmentRequests.CountAsync(r => r.MerchantId == id);

        return View(new MerchantDetailVm
        {
            Id = u.Id, FullName = u.FullName, PhoneNumber = u.PhoneNumber, Email = u.Email,
            IsActive = u.IsActive, IsBlocked = u.IsBlocked, BlockedAt = u.BlockedAt,
            BlockedReason = u.BlockedReason, CreatedAt = u.CreatedAt, ShipmentCount = count
        });
    }
}
```

> **NOTE on `ShipmentRequest.MerchantId`:** Verify this field name by opening `HM.Domain/Entities/ShipmentRequest.cs`. If the FK is named differently (e.g., `MerchantProfileId`), use that name instead. Build will fail until it matches.

- [ ] **Step 2: Build**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
```
If `MerchantId` doesn't exist, fix the field name per the NOTE above, then rebuild.

- [ ] **Step 3: Commit**

```bash
git add HM.AdminPanel/Controllers/MerchantsController.cs
git commit -m "feat(admin): MerchantsController (Index + Details)"
```

---

### Task F3: Merchants views + Block/Unblock/Verify actions

**Files:**
- Create: `HM.AdminPanel/Views/Merchants/Index.cshtml`
- Create: `HM.AdminPanel/Views/Merchants/Details.cshtml`
- Modify: `HM.AdminPanel/Controllers/MerchantsController.cs`

- [ ] **Step 1: Append three POST actions to `MerchantsController`**

```csharp
[HttpPost("Block/{id:guid}"), ValidateAntiForgeryToken]
[Authorize(Policy = AdminPolicies.RequireWriteAccess)]
public async Task<IActionResult> Block(Guid id, string? reason)
{
    var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Merchant);
    if (u is null) return NotFound();
    u.IsBlocked = true;
    u.BlockedAt = DateTime.UtcNow;
    u.BlockedReason = reason;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Merchant blocked.";
    return RedirectToAction(nameof(Details), new { id });
}

[HttpPost("Unblock/{id:guid}"), ValidateAntiForgeryToken]
[Authorize(Policy = AdminPolicies.RequireWriteAccess)]
public async Task<IActionResult> Unblock(Guid id)
{
    var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Merchant);
    if (u is null) return NotFound();
    u.IsBlocked = false;
    u.BlockedAt = null;
    u.BlockedReason = null;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Merchant unblocked.";
    return RedirectToAction(nameof(Details), new { id });
}

[HttpPost("Verify/{id:guid}"), ValidateAntiForgeryToken]
[Authorize(Policy = AdminPolicies.RequireWriteAccess)]
public async Task<IActionResult> Verify(Guid id)
{
    var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.UserType == UserType.Merchant);
    if (u is null) return NotFound();
    u.IsActive = true;
    u.IsOtpVerified = true;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Merchant verified.";
    return RedirectToAction(nameof(Details), new { id });
}
```

- [ ] **Step 2: `Views/Merchants/Index.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Merchants.MerchantListVm

<form method="get" class="card card-body mb-3">
    <div class="row">
        <div class="col-md-5">
            <input asp-for="Filter.Search" class="form-control" placeholder="Search name / phone / email" />
        </div>
        <div class="col-md-3">
            <select asp-for="Filter.IsBlocked" class="form-control">
                <option value="">— blocked? —</option>
                <option value="true">Blocked</option>
                <option value="false">Not blocked</option>
            </select>
        </div>
        <div class="col-md-2"><button class="btn btn-primary">Search</button></div>
    </div>
</form>

<table class="table table-striped" id="grid">
    <thead><tr>
        <th>Name</th><th>Phone</th><th>Email</th>
        <th>Active</th><th>Blocked</th><th>Created</th><th></th>
    </tr></thead>
    <tbody>
        @foreach (var m in Model.Result.Items)
        {
            <tr>
                <td>@m.FullName</td><td>@m.PhoneNumber</td><td>@m.Email</td>
                <td>@(m.IsActive ? "Yes" : "No")</td>
                <td>@(m.IsBlocked ? "Yes" : "No")</td>
                <td>@m.CreatedAt.ToString("u")</td>
                <td><a class="btn btn-sm btn-outline-primary" asp-action="Details" asp-route-id="@m.Id">Open</a></td>
            </tr>
        }
    </tbody>
</table>

@{ var pager = new HM.AdminPanel.ViewModels.Common.PagedResult<object>
   { Page = Model.Result.Page, PageSize = Model.Result.PageSize, Total = Model.Result.Total }; }
@await Html.PartialAsync("_Pager", pager)
```

- [ ] **Step 3: `Views/Merchants/Details.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Merchants.MerchantDetailVm

<div class="card"><div class="card-body">
    <dl class="row">
        <dt class="col-sm-3">Name</dt>        <dd class="col-sm-9">@Model.FullName</dd>
        <dt class="col-sm-3">Phone</dt>       <dd class="col-sm-9">@Model.PhoneNumber</dd>
        <dt class="col-sm-3">Email</dt>       <dd class="col-sm-9">@Model.Email</dd>
        <dt class="col-sm-3">Active</dt>      <dd class="col-sm-9">@(Model.IsActive ? "Yes" : "No")</dd>
        <dt class="col-sm-3">Blocked</dt>     <dd class="col-sm-9">@(Model.IsBlocked ? $"Yes — {Model.BlockedReason}" : "No")</dd>
        <dt class="col-sm-3">Shipments</dt>   <dd class="col-sm-9">@Model.ShipmentCount</dd>
        <dt class="col-sm-3">Created</dt>     <dd class="col-sm-9">@Model.CreatedAt.ToString("u")</dd>
    </dl>
</div></div>

<div class="mt-3 d-flex" style="gap:8px;">
    @if (!Model.IsBlocked)
    {
        <form method="post" asp-action="Block" asp-route-id="@Model.Id" data-confirm="Block this merchant?" class="d-flex" style="gap:6px;">
            @Html.AntiForgeryToken()
            <input name="reason" class="form-control" placeholder="Reason" />
            <button class="btn btn-danger">Block</button>
        </form>
    }
    else
    {
        <form method="post" asp-action="Unblock" asp-route-id="@Model.Id" data-confirm="Unblock?">
            @Html.AntiForgeryToken()
            <button class="btn btn-success">Unblock</button>
        </form>
    }
    @if (!Model.IsActive)
    {
        <form method="post" asp-action="Verify" asp-route-id="@Model.Id" data-confirm="Verify (activate)?">
            @Html.AntiForgeryToken()
            <button class="btn btn-primary">Verify</button>
        </form>
    }
</div>
```

- [ ] **Step 4: Build + smoke test**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
dotnet run --project HM.AdminPanel
```
- `/Merchants` lists merchants; search works; pager works.
- `/Merchants/<id>` opens details.
- Block / Unblock / Verify each show a SweetAlert confirm and update state.
- A new `AdminAuditLog` row exists for each POST (check Postgres).

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/Views/Merchants/ HM.AdminPanel/Controllers/MerchantsController.cs
git commit -m "feat(admin): Merchants Index/Details + Block/Unblock/Verify"
```

---

### Task F4: Drivers controller + views + actions

**Files:**
- Create: `HM.AdminPanel/ViewModels/Drivers/DriverFilterVm.cs`, `DriverListItemVm.cs`, `DriverListVm.cs`, `DriverDetailVm.cs`
- Create: `HM.AdminPanel/Controllers/DriversController.cs`
- Create: `HM.AdminPanel/Views/Drivers/Index.cshtml`, `Details.cshtml`

- [ ] **Step 1: Mirror Task F1's four VMs in the `Drivers` folder.** Same shape, replace `Merchant` with `Driver` in the file/class names, drop `ShipmentCount` from `DriverDetailVm` and replace with `int CompletedShipmentCount`.

- [ ] **Step 2: Mirror Task F2's controller as `DriversController`**, querying `UserType.Driver`. For completed shipments, count from `_db.Shipments.Where(s => s.DriverProfileId == id && s.Status == ShipmentStatus.Completed)`.

- [ ] **Step 3: Mirror Task F3's two views and three POST actions** under `Drivers/`. Identical Razor markup except for the heading text and the link target (`asp-controller="Drivers"`).

- [ ] **Step 4: Build + smoke test**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
dotnet run --project HM.AdminPanel
```
Verify `/Drivers` lists, details, block/unblock/verify all work.

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/ViewModels/Drivers/ HM.AdminPanel/Controllers/DriversController.cs HM.AdminPanel/Views/Drivers/
git commit -m "feat(admin): Drivers management (list/details/block/unblock/verify)"
```

---

### Task F5: TruckAccounts controller + views + actions

**Files:**
- Same shape as Task F4, in the `TruckAccounts/` folders.

- [ ] **Step 1: Create the four VMs** (`TruckAccountFilterVm`, `…ListItemVm`, `…ListVm`, `…DetailVm`). In `…DetailVm` replace `ShipmentCount` with `int OwnedTrucks` and `int DriversUnderAccount`.

- [ ] **Step 2: Create `TruckAccountsController`** mirroring `MerchantsController` but querying `UserType.TruckAccount`. Compute `OwnedTrucks = _db.Trucks.Where(t => t.TruckAccountId == id).Count()` and `DriversUnderAccount = _db.DriverProfiles.Where(d => d.TruckAccountId == id).Count()` (verify FK names in those entities — adjust if different).

- [ ] **Step 3: Create the two views** (Index, Details) mirroring Task F3.

- [ ] **Step 4: Build + smoke test**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
dotnet run --project HM.AdminPanel
```

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/ViewModels/TruckAccounts/ HM.AdminPanel/Controllers/TruckAccountsController.cs HM.AdminPanel/Views/TruckAccounts/
git commit -m "feat(admin): TruckAccounts management (list/details/block/unblock/verify)"
```

---

## Phase G — Module 3: Trucks Management

### Task G1: Trucks VMs

**Files:**
- Create: `HM.AdminPanel/ViewModels/Trucks/TruckFilterVm.cs`
- Create: `HM.AdminPanel/ViewModels/Trucks/TruckListItemVm.cs`
- Create: `HM.AdminPanel/ViewModels/Trucks/TruckListVm.cs`
- Create: `HM.AdminPanel/ViewModels/Trucks/TruckDetailVm.cs`

- [ ] **Step 1: `TruckFilterVm`**

```csharp
using HM.Domain.Enums;
namespace HM.AdminPanel.ViewModels.Trucks;
public class TruckFilterVm
{
    public TruckApprovalStatus? Status { get; set; }
    public string?              Search { get; set; }   // plate, owner phone
    public int                  Page     { get; set; } = 1;
    public int                  PageSize { get; set; } = 25;
}
```

- [ ] **Step 2: `TruckListItemVm`**

```csharp
using HM.Domain.Enums;
namespace HM.AdminPanel.ViewModels.Trucks;
public class TruckListItemVm
{
    public Guid                Id              { get; set; }
    public string              PlateNumber     { get; set; } = "";
    public TruckType           TruckType       { get; set; }
    public TruckBodyType?      BodyType        { get; set; }
    public decimal             MaxWeight       { get; set; }
    public TruckApprovalStatus ApprovalStatus  { get; set; }
    public bool                IsActive        { get; set; }
    public string              OwnerName       { get; set; } = "";
}
```

- [ ] **Step 3: `TruckListVm`**

```csharp
using HM.AdminPanel.ViewModels.Common;
namespace HM.AdminPanel.ViewModels.Trucks;
public class TruckListVm
{
    public TruckFilterVm Filter { get; set; } = new();
    public PagedResult<TruckListItemVm> Result { get; set; } = new();
}
```

- [ ] **Step 4: `TruckDetailVm`**

```csharp
using HM.Domain.Enums;
namespace HM.AdminPanel.ViewModels.Trucks;
public class TruckDetailVm
{
    public Guid                Id              { get; set; }
    public string              PlateNumber     { get; set; } = "";
    public TruckType           TruckType       { get; set; }
    public TruckBodyType?      BodyType        { get; set; }
    public decimal             MaxWeight       { get; set; }
    public bool                IsActive        { get; set; }
    public TruckApprovalStatus ApprovalStatus  { get; set; }
    public string?             RejectionReason { get; set; }
    public Guid                TruckAccountId  { get; set; }
    public string              OwnerName       { get; set; } = "";
    public int                 ShipmentsCount  { get; set; }
}
```

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/ViewModels/Trucks/
git commit -m "feat(admin): Trucks VMs"
```

---

### Task G2: `TrucksController`

**Files:**
- Create: `HM.AdminPanel/Controllers/TrucksController.cs`

- [ ] **Step 1: Controller**

```csharp
// HM.AdminPanel/Controllers/TrucksController.cs
using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.Common;
using HM.AdminPanel.ViewModels.Trucks;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class TrucksController : Controller
{
    private readonly ApplicationDbContext _db;
    public TrucksController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] TruckFilterVm filter)
    {
        ViewData["Title"] = "Trucks";

        var q = from t in _db.Trucks
                join u in _db.Users on t.TruckAccountId equals u.Id into uj
                from u in uj.DefaultIfEmpty()
                select new { t, u };

        if (filter.Status.HasValue)
            q = q.Where(x => x.t.ApprovalStatus == filter.Status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(x => x.t.PlateNumber.Contains(s)
                          || (x.u != null && (x.u.PhoneNumber.Contains(s) || x.u.FullName.Contains(s))));
        }

        var total = await q.CountAsync();
        var items = await q
            .OrderBy(x => x.t.ApprovalStatus).ThenBy(x => x.t.PlateNumber)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(x => new TruckListItemVm
            {
                Id = x.t.Id, PlateNumber = x.t.PlateNumber, TruckType = x.t.TruckType,
                BodyType = x.t.BodyType, MaxWeight = x.t.MaxWeight,
                ApprovalStatus = x.t.ApprovalStatus, IsActive = x.t.IsActive,
                OwnerName = x.u != null ? x.u.FullName : ""
            })
            .ToListAsync();

        return View(new TruckListVm
        {
            Filter = filter,
            Result = new PagedResult<TruckListItemVm>
            {
                Items = items, Page = filter.Page, PageSize = filter.PageSize, Total = total
            }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Truck";
        var x = await (from t in _db.Trucks
                       join u in _db.Users on t.TruckAccountId equals u.Id into uj
                       from u in uj.DefaultIfEmpty()
                       where t.Id == id
                       select new { t, u }).FirstOrDefaultAsync();
        if (x is null) return NotFound();

        var shipments = await _db.Shipments.CountAsync(s => s.TruckId == id);

        return View(new TruckDetailVm
        {
            Id = x.t.Id, PlateNumber = x.t.PlateNumber, TruckType = x.t.TruckType,
            BodyType = x.t.BodyType, MaxWeight = x.t.MaxWeight,
            IsActive = x.t.IsActive, ApprovalStatus = x.t.ApprovalStatus,
            RejectionReason = x.t.RejectionReason, TruckAccountId = x.t.TruckAccountId,
            OwnerName = x.u != null ? x.u.FullName : "", ShipmentsCount = shipments
        });
    }

    [HttpPost("Approve/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Approve(Guid id)
    {
        var t = await _db.Trucks.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.ApprovalStatus = TruckApprovalStatus.Approved;
        t.RejectionReason = null;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck approved.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Reject/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Reject(Guid id, string? reason)
    {
        var t = await _db.Trucks.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.ApprovalStatus = TruckApprovalStatus.Rejected;
        t.RejectionReason = reason;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck rejected.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("Suspend/{id:guid}"), ValidateAntiForgeryToken]
    [Authorize(Policy = AdminPolicies.RequireWriteAccess)]
    public async Task<IActionResult> Suspend(Guid id)
    {
        var t = await _db.Trucks.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.IsActive = false;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Truck suspended.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Controllers/TrucksController.cs
git commit -m "feat(admin): TrucksController (list/details/approve/reject/suspend)"
```

---

### Task G3: Trucks views

**Files:**
- Create: `HM.AdminPanel/Views/Trucks/Index.cshtml`
- Create: `HM.AdminPanel/Views/Trucks/Details.cshtml`

- [ ] **Step 1: `Index.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Trucks.TruckListVm
@using HM.Domain.Enums

<form method="get" class="card card-body mb-3">
    <div class="row">
        <div class="col-md-4">
            <input asp-for="Filter.Search" class="form-control" placeholder="Plate / owner name / phone" />
        </div>
        <div class="col-md-3">
            <select asp-for="Filter.Status" class="form-control">
                <option value="">— status —</option>
                @foreach (var s in Enum.GetValues<TruckApprovalStatus>())
                {
                    <option value="@s">@s</option>
                }
            </select>
        </div>
        <div class="col-md-2"><button class="btn btn-primary">Search</button></div>
    </div>
</form>

<table class="table table-striped">
    <thead><tr><th>Plate</th><th>Type</th><th>Body</th><th>Weight</th><th>Owner</th><th>Status</th><th>Active</th><th></th></tr></thead>
    <tbody>
        @foreach (var t in Model.Result.Items)
        {
            <tr>
                <td>@t.PlateNumber</td>
                <td>@t.TruckType</td>
                <td>@t.BodyType</td>
                <td>@t.MaxWeight</td>
                <td>@t.OwnerName</td>
                <td>@t.ApprovalStatus</td>
                <td>@(t.IsActive ? "Yes" : "No")</td>
                <td><a class="btn btn-sm btn-outline-primary" asp-action="Details" asp-route-id="@t.Id">Open</a></td>
            </tr>
        }
    </tbody>
</table>

@{ var pager = new HM.AdminPanel.ViewModels.Common.PagedResult<object>
   { Page = Model.Result.Page, PageSize = Model.Result.PageSize, Total = Model.Result.Total }; }
@await Html.PartialAsync("_Pager", pager)
```

- [ ] **Step 2: `Details.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Trucks.TruckDetailVm
@using HM.Domain.Enums

<div class="card"><div class="card-body">
    <dl class="row">
        <dt class="col-sm-3">Plate</dt>    <dd class="col-sm-9">@Model.PlateNumber</dd>
        <dt class="col-sm-3">Type</dt>     <dd class="col-sm-9">@Model.TruckType / @Model.BodyType</dd>
        <dt class="col-sm-3">Max weight</dt><dd class="col-sm-9">@Model.MaxWeight</dd>
        <dt class="col-sm-3">Owner</dt>    <dd class="col-sm-9">@Model.OwnerName (@Model.TruckAccountId)</dd>
        <dt class="col-sm-3">Status</dt>   <dd class="col-sm-9">@Model.ApprovalStatus @(Model.RejectionReason is null ? "" : $"— {Model.RejectionReason}")</dd>
        <dt class="col-sm-3">Active</dt>   <dd class="col-sm-9">@(Model.IsActive ? "Yes" : "No")</dd>
        <dt class="col-sm-3">Shipments</dt><dd class="col-sm-9">@Model.ShipmentsCount</dd>
    </dl>
</div></div>

<div class="mt-3 d-flex" style="gap:8px;">
    @if (Model.ApprovalStatus != TruckApprovalStatus.Approved)
    {
        <form method="post" asp-action="Approve" asp-route-id="@Model.Id" data-confirm="Approve truck?">
            @Html.AntiForgeryToken()
            <button class="btn btn-success">Approve</button>
        </form>
    }
    @if (Model.ApprovalStatus != TruckApprovalStatus.Rejected)
    {
        <form method="post" asp-action="Reject" asp-route-id="@Model.Id" data-confirm="Reject truck?" class="d-flex" style="gap:6px;">
            @Html.AntiForgeryToken()
            <input name="reason" class="form-control" placeholder="Reason" />
            <button class="btn btn-danger">Reject</button>
        </form>
    }
    @if (Model.IsActive)
    {
        <form method="post" asp-action="Suspend" asp-route-id="@Model.Id" data-confirm="Suspend truck?">
            @Html.AntiForgeryToken()
            <button class="btn btn-warning">Suspend</button>
        </form>
    }
</div>
```

- [ ] **Step 3: Build + smoke test**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
dotnet run --project HM.AdminPanel
```
Verify `/Trucks` lists, filter by status works, details + approve/reject/suspend work, audit rows appear.

- [ ] **Step 4: Commit**

```bash
git add HM.AdminPanel/Views/Trucks/
git commit -m "feat(admin): Trucks Index/Details views"
```

---

## Phase H — Module 4: Shipments + Shipment Requests

### Task H1: Shipments VMs

**Files:**
- Create: `HM.AdminPanel/ViewModels/Shipments/ShipmentFilterVm.cs`
- Create: `HM.AdminPanel/ViewModels/Shipments/ShipmentListItemVm.cs`
- Create: `HM.AdminPanel/ViewModels/Shipments/ShipmentListVm.cs`
- Create: `HM.AdminPanel/ViewModels/Shipments/ShipmentDetailVm.cs`

- [ ] **Step 1: `ShipmentFilterVm`**

```csharp
using HM.Domain.Enums;
namespace HM.AdminPanel.ViewModels.Shipments;
public class ShipmentFilterVm
{
    public ShipmentStatus? Status   { get; set; }
    public DateTime?       From     { get; set; }
    public DateTime?       To       { get; set; }
    public Guid?           MerchantId{ get; set; }
    public Guid?           DriverId { get; set; }
    public int             Page     { get; set; } = 1;
    public int             PageSize { get; set; } = 25;
}
```

- [ ] **Step 2: `ShipmentListItemVm`**

```csharp
using HM.Domain.Enums;
namespace HM.AdminPanel.ViewModels.Shipments;
public class ShipmentListItemVm
{
    public Guid           Id          { get; set; }
    public ShipmentStatus Status      { get; set; }
    public DateTime?      StartedAt   { get; set; }
    public DateTime?      CompletedAt { get; set; }
    public Guid           TruckId     { get; set; }
    public Guid?          DriverId    { get; set; }
}
```

- [ ] **Step 3: `ShipmentListVm`**

```csharp
using HM.AdminPanel.ViewModels.Common;
namespace HM.AdminPanel.ViewModels.Shipments;
public class ShipmentListVm
{
    public ShipmentFilterVm Filter { get; set; } = new();
    public PagedResult<ShipmentListItemVm> Result { get; set; } = new();
}
```

- [ ] **Step 4: `ShipmentDetailVm`**

```csharp
using HM.Domain.Enums;
namespace HM.AdminPanel.ViewModels.Shipments;
public class ShipmentDetailVm
{
    public Guid           Id              { get; set; }
    public ShipmentStatus Status          { get; set; }
    public DateTime?      StartedAt       { get; set; }
    public DateTime?      CompletedAt     { get; set; }
    public DateTime?      AssignedAt      { get; set; }
    public double?        CurrentLat      { get; set; }
    public double?        CurrentLng      { get; set; }
    public DateTime?      LocationUpdatedAt{ get; set; }
    public Guid           ShipmentRequestId{ get; set; }
    public Guid           AcceptedOfferId { get; set; }
    public Guid           TruckId         { get; set; }
    public Guid?          DriverProfileId { get; set; }
}
```

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/ViewModels/Shipments/
git commit -m "feat(admin): Shipments VMs"
```

---

### Task H2: `ShipmentsController` (Index + Details)

**Files:**
- Create: `HM.AdminPanel/Controllers/ShipmentsController.cs`

- [ ] **Step 1: Controller (read actions)**

```csharp
// HM.AdminPanel/Controllers/ShipmentsController.cs
using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.Common;
using HM.AdminPanel.ViewModels.Shipments;
using HM.Domain.Enums;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class ShipmentsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ShipmentsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ShipmentFilterVm filter)
    {
        ViewData["Title"] = "Shipments";

        var q = _db.Shipments.AsQueryable();
        if (filter.Status.HasValue) q = q.Where(s => s.Status == filter.Status);
        if (filter.From.HasValue)   q = q.Where(s => s.StartedAt >= filter.From);
        if (filter.To.HasValue)     q = q.Where(s => s.StartedAt <= filter.To);
        if (filter.DriverId.HasValue) q = q.Where(s => s.DriverProfileId == filter.DriverId);

        var total = await q.CountAsync();
        var items = await q
            .OrderByDescending(s => s.StartedAt ?? DateTime.MinValue)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(s => new ShipmentListItemVm
            {
                Id = s.Id, Status = s.Status, StartedAt = s.StartedAt,
                CompletedAt = s.CompletedAt, TruckId = s.TruckId,
                DriverId = s.DriverProfileId
            })
            .ToListAsync();

        return View(new ShipmentListVm
        {
            Filter = filter,
            Result = new PagedResult<ShipmentListItemVm>
            {
                Items = items, Page = filter.Page, PageSize = filter.PageSize, Total = total
            }
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id)
    {
        ViewData["Title"] = "Shipment";
        var s = await _db.Shipments.FirstOrDefaultAsync(x => x.Id == id);
        if (s is null) return NotFound();

        return View(new ShipmentDetailVm
        {
            Id = s.Id, Status = s.Status, StartedAt = s.StartedAt,
            CompletedAt = s.CompletedAt, AssignedAt = s.AssignedAt,
            CurrentLat = s.CurrentLat, CurrentLng = s.CurrentLng,
            LocationUpdatedAt = s.LocationUpdatedAt,
            ShipmentRequestId = s.ShipmentRequestId, AcceptedOfferId = s.AcceptedOfferId,
            TruckId = s.TruckId, DriverProfileId = s.DriverProfileId
        });
    }
}
```

- [ ] **Step 2: Build + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/Controllers/ShipmentsController.cs
git commit -m "feat(admin): ShipmentsController (Index + Details)"
```

---

### Task H3: Shipments views + Cancel/Reassign

**Files:**
- Create: `HM.AdminPanel/Views/Shipments/Index.cshtml`
- Create: `HM.AdminPanel/Views/Shipments/Details.cshtml`
- Modify: `HM.AdminPanel/Controllers/ShipmentsController.cs`

- [ ] **Step 1: Append POST actions to `ShipmentsController`**

```csharp
[HttpPost("Cancel/{id:guid}"), ValidateAntiForgeryToken]
[Authorize(Policy = AdminPolicies.RequireWriteAccess)]
public async Task<IActionResult> Cancel(Guid id, string? reason)
{
    var s = await _db.Shipments.FirstOrDefaultAsync(x => x.Id == id);
    if (s is null) return NotFound();
    s.Status = ShipmentStatus.Cancelled;
    s.CompletedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();
    TempData["Success"] = $"Shipment cancelled. Reason: {reason ?? "(none)"}";
    return RedirectToAction(nameof(Details), new { id });
}

[HttpPost("Reassign/{id:guid}"), ValidateAntiForgeryToken]
[Authorize(Policy = AdminPolicies.RequireWriteAccess)]
public async Task<IActionResult> Reassign(Guid id, Guid newDriverId)
{
    var s = await _db.Shipments.FirstOrDefaultAsync(x => x.Id == id);
    if (s is null) return NotFound();
    s.DriverProfileId = newDriverId;
    s.AssignedAt = DateTime.UtcNow;
    await _db.SaveChangesAsync();
    TempData["Success"] = "Driver reassigned.";
    return RedirectToAction(nameof(Details), new { id });
}
```

> **NOTE:** This reassign does not notify the driver via push. Driver notification is out of v1 scope (kept simple by design); a follow-up task can hook into `INotificationService`.

- [ ] **Step 2: `Index.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Shipments.ShipmentListVm
@using HM.Domain.Enums

<form method="get" class="card card-body mb-3">
    <div class="row">
        <div class="col-md-3">
            <select asp-for="Filter.Status" class="form-control">
                <option value="">— status —</option>
                @foreach (var s in Enum.GetValues<ShipmentStatus>())
                {
                    <option value="@s">@s</option>
                }
            </select>
        </div>
        <div class="col-md-3"><input asp-for="Filter.From" type="date" class="form-control" /></div>
        <div class="col-md-3"><input asp-for="Filter.To" type="date" class="form-control" /></div>
        <div class="col-md-2"><button class="btn btn-primary">Search</button></div>
    </div>
</form>

<table class="table table-striped">
    <thead><tr><th>Id</th><th>Status</th><th>Started</th><th>Completed</th><th>Truck</th><th>Driver</th><th></th></tr></thead>
    <tbody>
        @foreach (var s in Model.Result.Items)
        {
            <tr>
                <td><code>@s.Id</code></td>
                <td>@s.Status</td>
                <td>@s.StartedAt?.ToString("u")</td>
                <td>@s.CompletedAt?.ToString("u")</td>
                <td><code>@s.TruckId</code></td>
                <td>@(s.DriverId?.ToString() ?? "—")</td>
                <td><a class="btn btn-sm btn-outline-primary" asp-action="Details" asp-route-id="@s.Id">Open</a></td>
            </tr>
        }
    </tbody>
</table>

@{ var pager = new HM.AdminPanel.ViewModels.Common.PagedResult<object>
   { Page = Model.Result.Page, PageSize = Model.Result.PageSize, Total = Model.Result.Total }; }
@await Html.PartialAsync("_Pager", pager)
```

- [ ] **Step 3: `Details.cshtml`**

```cshtml
@model HM.AdminPanel.ViewModels.Shipments.ShipmentDetailVm
@using HM.Domain.Enums

<div class="card"><div class="card-body">
    <dl class="row">
        <dt class="col-sm-3">Status</dt>      <dd class="col-sm-9">@Model.Status</dd>
        <dt class="col-sm-3">Started</dt>     <dd class="col-sm-9">@Model.StartedAt?.ToString("u")</dd>
        <dt class="col-sm-3">Completed</dt>   <dd class="col-sm-9">@Model.CompletedAt?.ToString("u")</dd>
        <dt class="col-sm-3">Truck</dt>       <dd class="col-sm-9"><code>@Model.TruckId</code></dd>
        <dt class="col-sm-3">Driver</dt>      <dd class="col-sm-9">@(Model.DriverProfileId?.ToString() ?? "—")</dd>
        <dt class="col-sm-3">Current loc</dt> <dd class="col-sm-9">@(Model.CurrentLat),@(Model.CurrentLng) @(Model.LocationUpdatedAt?.ToString("u"))</dd>
    </dl>
</div></div>

@section Head { <link rel="stylesheet" href="~/leaflet/leaflet.css" /> }

@if (Model.CurrentLat.HasValue && Model.CurrentLng.HasValue)
{
    <div id="map" style="height:400px;" class="mt-3"></div>
}

<div class="mt-3 d-flex" style="gap:8px;">
    @if (Model.Status != ShipmentStatus.Completed && Model.Status != ShipmentStatus.Cancelled)
    {
        <form method="post" asp-action="Cancel" asp-route-id="@Model.Id" data-confirm="Cancel this shipment?" class="d-flex" style="gap:6px;">
            @Html.AntiForgeryToken()
            <input name="reason" class="form-control" placeholder="Reason" />
            <button class="btn btn-danger">Cancel</button>
        </form>

        <form method="post" asp-action="Reassign" asp-route-id="@Model.Id" data-confirm="Reassign driver?" class="d-flex" style="gap:6px;">
            @Html.AntiForgeryToken()
            <input name="newDriverId" class="form-control" placeholder="New driver Guid" />
            <button class="btn btn-warning">Reassign</button>
        </form>
    }
</div>

@section Scripts {
    <script src="~/leaflet/leaflet.js"></script>
    @if (Model.CurrentLat.HasValue && Model.CurrentLng.HasValue)
    {
        <text>
        <script>
            const lat = @Model.CurrentLat, lng = @Model.CurrentLng;
            const map = L.map('map').setView([lat, lng], 13);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OpenStreetMap' }).addTo(map);
            L.marker([lat, lng]).addTo(map);
        </script>
        </text>
    }
}
```

- [ ] **Step 4: Build + smoke test**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
dotnet run --project HM.AdminPanel
```
Verify list/details/cancel/reassign all work. Cancel produces an `AdminAuditLog` row.

- [ ] **Step 5: Commit**

```bash
git add HM.AdminPanel/Views/Shipments/ HM.AdminPanel/Controllers/ShipmentsController.cs
git commit -m "feat(admin): Shipments Index/Details + Cancel/Reassign"
```

---

### Task H4: `ShipmentRequestsController` (read-only list)

**Files:**
- Create: `HM.AdminPanel/ViewModels/ShipmentRequests/ShipmentRequestListItemVm.cs`
- Create: `HM.AdminPanel/Controllers/ShipmentRequestsController.cs`
- Create: `HM.AdminPanel/Views/ShipmentRequests/Index.cshtml`

- [ ] **Step 1: List item VM**

```csharp
// HM.AdminPanel/ViewModels/ShipmentRequests/ShipmentRequestListItemVm.cs
using HM.Domain.Enums;
namespace HM.AdminPanel.ViewModels.ShipmentRequests;
public class ShipmentRequestListItemVm
{
    public Guid                   Id        { get; set; }
    public ShipmentRequestStatus  Status    { get; set; }
    public Guid                   MerchantId{ get; set; }
    public DateTime               CreatedAt { get; set; }
    public int                    OffersCount { get; set; }
}
```

- [ ] **Step 2: Controller**

```csharp
// HM.AdminPanel/Controllers/ShipmentRequestsController.cs
using HM.AdminPanel.Authorization;
using HM.AdminPanel.ViewModels.ShipmentRequests;
using HM.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HM.AdminPanel.Controllers;

[Authorize(Policy = AdminPolicies.RequireAdmin)]
public class ShipmentRequestsController : Controller
{
    private readonly ApplicationDbContext _db;
    public ShipmentRequestsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Shipment Requests";
        // Note: verify the FK property name (MerchantId vs MerchantProfileId) before relying on it.
        var items = await _db.ShipmentRequests
            .OrderByDescending(r => r.Id)
            .Take(200)
            .Select(r => new ShipmentRequestListItemVm
            {
                Id = r.Id,
                Status = r.Status,
                MerchantId = r.MerchantId,
                CreatedAt = r.CreatedAt,
                OffersCount = _db.ShipmentOffers.Count(o => o.ShipmentRequestId == r.Id)
            })
            .ToListAsync();
        return View(items);
    }
}
```

> **NOTE:** If `ShipmentRequest` doesn't expose `MerchantId` and `CreatedAt` (open the file), substitute the actual field names. Same for `ShipmentOffer.ShipmentRequestId`.

- [ ] **Step 3: `Index.cshtml`**

```cshtml
@model List<HM.AdminPanel.ViewModels.ShipmentRequests.ShipmentRequestListItemVm>

<table class="table table-striped">
    <thead><tr><th>Id</th><th>Status</th><th>Merchant</th><th>Created</th><th>Offers</th></tr></thead>
    <tbody>
        @foreach (var r in Model)
        {
            <tr>
                <td><code>@r.Id</code></td>
                <td>@r.Status</td>
                <td><code>@r.MerchantId</code></td>
                <td>@r.CreatedAt.ToString("u")</td>
                <td>@r.OffersCount</td>
            </tr>
        }
    </tbody>
</table>
```

- [ ] **Step 4: Build + smoke test + commit**

```bash
dotnet build HM.AdminPanel/HM.AdminPanel.csproj
git add HM.AdminPanel/ViewModels/ShipmentRequests/ HM.AdminPanel/Controllers/ShipmentRequestsController.cs HM.AdminPanel/Views/ShipmentRequests/
git commit -m "feat(admin): ShipmentRequests read-only list"
```

---

## Phase I — Deployment & smoke checklist

### Task I1: Update `scripts/deploy.sh`

**Files:**
- Modify: `scripts/deploy.sh`

- [ ] **Step 1: Open `scripts/deploy.sh` and find the section that publishes `Hm.WebApi`.** Right after that block, add a parallel block for `HM.AdminPanel`:

```bash
# Publish HM.AdminPanel
dotnet publish HM.AdminPanel/HM.AdminPanel.csproj -c Release -o /tmp/hm-admin-publish
sudo systemctl stop hm-admin || true
sudo rsync -a --delete --exclude='appsettings.json' /tmp/hm-admin-publish/ /var/www/hm-admin/
sudo systemctl start hm-admin
```

Place the AdminPanel start step AFTER WebApi health-check passes, so a broken admin doesn't roll back the API.

- [ ] **Step 2: Add health-check for admin (HTTP 200 on `/Account/Login`)**

```bash
ADMIN_HEALTH=$(curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5050/Account/Login || true)
if [ "$ADMIN_HEALTH" != "200" ]; then
    echo "Admin health check failed (HTTP $ADMIN_HEALTH)"
    exit 1
fi
```

- [ ] **Step 3: Commit**

```bash
git add scripts/deploy.sh
git commit -m "chore(deploy): publish + health-check HM.AdminPanel"
```

---

### Task I2: Document systemd unit + nginx snippet

**Files:**
- Create: `docs/admin-panel-systemd.md`

- [ ] **Step 1: Create the doc**

````markdown
# HM Admin Panel — server config

## systemd unit (`/etc/systemd/system/hm-admin.service`)

```ini
[Unit]
Description=HM Admin Panel
After=network.target

[Service]
WorkingDirectory=/var/www/hm-admin
ExecStart=/usr/bin/dotnet /var/www/hm-admin/HM.AdminPanel.dll --urls=http://127.0.0.1:5050
Restart=always
RestartSec=5
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

```bash
sudo systemctl enable hm-admin
sudo systemctl start hm-admin
```

## nginx (`/etc/nginx/sites-available/admin.hm.fustani.cloud`)

```nginx
server {
    listen 443 ssl http2;
    server_name admin.hm.fustani.cloud;

    ssl_certificate     /etc/letsencrypt/live/admin.hm.fustani.cloud/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/admin.hm.fustani.cloud/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5050;
        proxy_http_version 1.1;
        proxy_set_header   Host              $host;
        proxy_set_header   X-Real-IP         $remote_addr;
        proxy_set_header   X-Forwarded-For   $proxy_add_x_forwarded_for;
        proxy_set_header   X-Forwarded-Proto $scheme;
    }
}
```

## Production `appsettings.json` on the box

Located at `/var/www/hm-admin/appsettings.json` (preserved across deploys). At minimum:

```json
{
  "ConnectionStrings": { "DefaultConnection": "<prod connection string>" },
  "AdminPanel": {
    "SignalRHubUrl": "https://hm.fustani.cloud/hubs/shipment-tracking"
  }
}
```
````

- [ ] **Step 2: Commit**

```bash
git add docs/admin-panel-systemd.md
git commit -m "docs(admin): systemd unit + nginx config"
```

---

### Task I3: Manual smoke-test checklist

**Files:**
- Create: `docs/admin-panel-smoke-test.md`

- [ ] **Step 1: Create checklist**

```markdown
# HM Admin Panel v1 — smoke test

Run after every release. Use seeded SuperAdmin and a separate Support account.

## Auth
- [ ] `/` redirects to `/Account/Login` when unauthenticated
- [ ] Invalid credentials → error message, row in `AdminLoginAttempts`
- [ ] 10 invalid attempts in 10 min → "too many attempts" message
- [ ] Valid SuperAdmin login → `/Dashboard`
- [ ] Logout → back to login, cookie cleared
- [ ] Direct GET of `/Merchants` while logged out → redirected to login

## Dashboard
- [ ] KPI cards show non-zero numbers (with seeded data)
- [ ] Line chart renders 30 days
- [ ] Doughnut chart renders status breakdown
- [ ] Recent activity table has rows
- [ ] `/Dashboard/LiveMap` opens, map tiles load
- [ ] If WebApi is running, SignalR connection opens (check browser console)

## Merchants / Drivers / TruckAccounts
- [ ] Index lists rows, paged, search by name/phone/email works
- [ ] Details opens
- [ ] Block requires reason, succeeds, sweetalert confirm appears
- [ ] Audit row written (query `AdminAuditLogs`)
- [ ] Unblock succeeds
- [ ] Verify activates user

## Trucks
- [ ] Filter by `Pending` shows only pending
- [ ] Approve → status flips
- [ ] Reject with reason → status + reason saved
- [ ] Suspend → `IsActive = false`

## Shipments
- [ ] Filter by status + date range works
- [ ] Details shows map if location present
- [ ] Cancel updates status to `Cancelled`
- [ ] Reassign updates `DriverProfileId`, `AssignedAt`

## Authorization
- [ ] ReadOnly account cannot see Block/Approve/Cancel buttons (or POST returns 403)
- [ ] Support account can write but cannot access `/Settings/Admins` (v2)
```

- [ ] **Step 2: Commit**

```bash
git add docs/admin-panel-smoke-test.md
git commit -m "docs(admin): v1 smoke test checklist"
```

---

## Self-Review (done — these are the gaps caught + fixed inline)

- **Spec coverage:** Modules 11 / 1 / 2 / 3 / 4 → Phases D / E / F / G / H. v2 modules (5–10) intentionally not in this plan; they get a separate plan once v1 is shipped.
- **Type consistency:** `AdminAuditLog.AdminUserId` is `Guid` (matches Identity's Guid key). `Truck.ApprovalStatus` mapped as string for portability. ViewModel filter/list/detail names follow a consistent pattern across modules.
- **Placeholder scan:** Two notes flag field-name verification (`ShipmentRequest.MerchantId`, `ShipmentRequest.CreatedAt`, `ShipmentOffer.ShipmentRequestId`, `DriverProfile.TruckAccountId`). These aren't placeholders — they're explicit checks the implementer must do before continuing; the code shown is the canonical form and the note tells them what to change if it doesn't match.
- **Tasks F4/F5 condensed:** Drivers and TruckAccounts re-use the Merchants shape end-to-end. The compressed format trades some duplicated Razor for plan readability; an implementer can copy the Merchants files and rename consistently in ~5 minutes per controller.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-05-24-admin-dashboard-v1.md`.**

Two execution options:

1. **Subagent-Driven (recommended)** — I dispatch a fresh subagent per task with full plan context, review the diff between tasks, fast iteration. Best for a plan this size because each subagent can focus on one task without dragging the whole plan into its context.

2. **Inline Execution** — execute tasks in this session using the executing-plans skill, batched with review checkpoints. Slower iteration, single context window.

**Which approach?**
