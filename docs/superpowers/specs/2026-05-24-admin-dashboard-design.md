# Admin Dashboard — Design Spec

**Date:** 2026-05-24
**Status:** Approved (brainstorming complete)
**Owner:** Ma7mood202

---

## 1. Goal

Add a server-rendered **admin dashboard** to the HM solution so internal staff can manage users, trucks, shipments, geography, notifications, settings, and audit/reporting — without touching the mobile-facing JSON API.

## 2. Scope

### v1 (Phase 1)
- Module 11 — Admin Authentication
- Module 1  — Dashboard / Overview
- Module 2  — Users Management (Merchants, Drivers, Truck Accounts)
- Module 3  — Trucks Management
- Module 4  — Shipments Management

### v2 (Phase 2)
- Module 5  — Driver Invitations
- Module 6  — Geography Management
- Module 7  — Notifications (broadcast + history)
- Module 8  — Reports & Exports (Excel/CSV)
- Module 9  — Admin Settings (app config + admin users CRUD)
- Module 10 — Audit & Logs

Both phases ship under the same project; v1 lands first, v2 follows. The spec covers both; the *implementation plan* will focus on v1.

## 3. Architecture & Project Layout

A new **`HM.AdminPanel`** ASP.NET Core 8 MVC project is added to `HM.sln`:

```
HM.sln
├── HM.Domain              ← unchanged
├── HM.Application         ← unchanged
├── HM.Infrastructure      ← +3 entities, +1 migration, role seeding
├── Hm.WebApi              ← unchanged (mobile API, JWT)
└── HM.AdminPanel          ← NEW (MVC + Razor Views, AdminLTE 3, cookie auth)
```

- `HM.AdminPanel` references `HM.Application` and `HM.Infrastructure`.
- **Two processes in production**: `Hm.WebApi` on its current port (mobile/JWT), `HM.AdminPanel` on a separate port (admin/cookies). Same SQL database.
- **No cross-app auth** — JWT only in WebApi, cookies only in AdminPanel.
- **SignalR**: the live shipment map page in AdminPanel connects to the existing `/hubs/shipment-tracking` on `Hm.WebApi` as a JS client (CORS already permissive).
- **Deployment**: `scripts/deploy.sh` adds publish + systemd service for `HM.AdminPanel` on port `5050`, reverse-proxied at `admin.hm.fustani.cloud` (or `/admin` path — TBD with infra).

## 4. Inside `HM.AdminPanel` — MVC Structure

```
HM.AdminPanel/
├── Controllers/
│   ├── AccountController.cs            ← Module 11 (login/logout)
│   ├── DashboardController.cs          ← Module 1
│   ├── MerchantsController.cs          ← Module 2
│   ├── DriversController.cs            ← Module 2
│   ├── TruckAccountsController.cs      ← Module 2
│   ├── TrucksController.cs             ← Module 3
│   ├── ShipmentsController.cs          ← Module 4
│   ├── ShipmentRequestsController.cs   ← Module 4
│   ├── InvitationsController.cs        ← Module 5
│   ├── GeographyController.cs          ← Module 6
│   ├── NotificationsController.cs      ← Module 7
│   ├── ReportsController.cs            ← Module 8
│   ├── SettingsController.cs           ← Module 9
│   └── AuditController.cs              ← Module 10
│
├── Views/
│   ├── Shared/ (_Layout, _LoginLayout, _Sidebar, _Pager, _ValidationScriptsPartial)
│   └── <ControllerName>/ (Index, Details, Edit, etc.)
│
├── ViewModels/<Feature>/ (per-page VMs: List, Detail, Filter, Edit)
│
├── Services/
│   ├── IAdminAuditLogger / AdminAuditLogger
│   ├── IExcelExportService / ExcelExportService   (ClosedXML)
│   └── IDashboardQueryService / DashboardQueryService
│
├── Authorization/
│   ├── AdminRoles.cs       (SuperAdmin, Support, ReadOnly)
│   ├── AdminPolicies.cs    (RequireAdmin, RequireSuperAdmin, RequireWriteAccess)
│   └── AuditActionFilter.cs (global, logs every POST/PUT/DELETE/PATCH)
│
├── Extensions/
│   ├── ServiceCollectionExtensions.cs   (AddAdminAuth, AddAdminServices)
│   └── HttpContextExtensions.cs         (CurrentAdminId, CurrentAdminRole)
│
├── wwwroot/
│   ├── adminlte/         ← AdminLTE 3 dist
│   ├── leaflet/          ← Leaflet + plugins
│   ├── signalr/          ← @microsoft/signalr browser
│   ├── lib/              ← jquery, datatables, chart.js, sweetalert2
│   └── css/site.css, js/site.js
│
├── Program.cs
├── appsettings.json
└── HM.AdminPanel.csproj
```

**Boundaries:**
- One controller per logical area; controllers stay thin.
- ViewModels never leak Domain entities into Views.
- Admin-only services live inside `HM.AdminPanel/Services`. UserManager, NotificationService, TruckService, etc. are reused from `HM.Infrastructure` via DI.
- `AuditActionFilter` ensures every state-changing request writes an audit row (Module 10 stays self-maintaining).

## 5. Data Model Changes

### New entities (`HM.Domain/Entities`)

```csharp
public class AdminAuditLog
{
    public Guid     Id          { get; set; }
    public string   AdminUserId { get; set; }
    public string   AdminEmail  { get; set; }
    public string   Action      { get; set; }   // "BlockMerchant", "CancelShipment", …
    public string   EntityType  { get; set; }   // "Merchant", "Shipment", …
    public string?  EntityId    { get; set; }
    public string?  Details     { get; set; }   // JSON blob
    public string?  IpAddress   { get; set; }
    public DateTime CreatedAt   { get; set; }
}

public class AdminLoginAttempt
{
    public Guid     Id        { get; set; }
    public string   Email     { get; set; }
    public bool     Success   { get; set; }
    public string?  IpAddress { get; set; }
    public string?  UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class AppSetting
{
    public string   Key         { get; set; }   // PK, e.g. "Commission.Percent"
    public string   Value       { get; set; }
    public string?  Description { get; set; }
    public DateTime UpdatedAt   { get; set; }
    public string?  UpdatedBy   { get; set; }
}
```

### Existing entities — additions

```csharp
// HM.Infrastructure/Identity/ApplicationUser.cs
public bool      IsBlocked     { get; set; }
public DateTime? BlockedAt     { get; set; }
public string?   BlockedReason { get; set; }

// HM.Domain/Enums/TruckApprovalStatus.cs  (new)
public enum TruckApprovalStatus { Pending, Approved, Rejected }

// HM.Domain/Entities/Truck.cs
public TruckApprovalStatus ApprovalStatus { get; set; }
public string?             RejectionReason { get; set; }
```

### Identity additions

- New parent role: `Admin`.
- Sub-roles: `SuperAdmin`, `Support`, `ReadOnly`.
- `DbSeeder` extended to seed a default SuperAdmin on first run, with credentials read from `appsettings.json` (`AdminPanel:SeedAdmin:Email` / `:Password`).
- All four roles seeded on migration.

### Migration

- One new EF migration: `AddAdminPanelTables` — creates the three new tables, adds new columns to `AspNetUsers` and `Trucks`, seeds the four roles.
- Runs automatically via existing `db.Database.MigrateAsync()` in `Hm.WebApi/Program.cs` on startup.

## 6. Authentication & Authorization

### Cookie auth (`HM.AdminPanel/Program.cs`)

```csharp
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
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
```

No JWT in the admin app. `UserManager<ApplicationUser>` is reused via `AddInfrastructure(...)`.

### Login flow

1. `GET /Account/Login` → renders login view (`_LoginLayout`).
2. `POST /Account/Login`:
   - Reject if user not found, `IsBlocked`, or not in `Admin` role.
   - Verify password via `SignInManager.CheckPasswordSignInAsync`.
   - On success: build `ClaimsPrincipal` with id, email, role claims → `HttpContext.SignInAsync`.
   - Write `AdminLoginAttempts` row regardless of success/failure.
3. `POST /Account/Logout` → `SignOutAsync` → redirect to login.

### Policies

```csharp
options.AddPolicy("RequireAdmin",
    p => p.RequireRole("SuperAdmin", "Support", "ReadOnly"));
options.AddPolicy("RequireSuperAdmin",
    p => p.RequireRole("SuperAdmin"));
options.AddPolicy("RequireWriteAccess",
    p => p.RequireRole("SuperAdmin", "Support"));
options.FallbackPolicy = options.GetPolicy("RequireAdmin");
```

### Role matrix

| Module                    | SuperAdmin | Support | ReadOnly |
|---------------------------|:---:|:---:|:---:|
| Dashboard view            |  ✅  |  ✅  |  ✅  |
| View users                |  ✅  |  ✅  |  ✅  |
| Block / unblock / verify  |  ✅  |  ✅  |  ❌  |
| View trucks               |  ✅  |  ✅  |  ✅  |
| Approve / reject trucks   |  ✅  |  ✅  |  ❌  |
| View shipments            |  ✅  |  ✅  |  ✅  |
| Cancel / reassign         |  ✅  |  ✅  |  ❌  |
| Invitations               |  ✅  |  ✅  |  view |
| Geography CRUD            |  ✅  |  ✅  |  ❌  |
| Notifications (send)      |  ✅  |  ✅  |  ❌  |
| Reports / export          |  ✅  |  ✅  |  ✅  |
| App settings              |  ✅  |  ❌  |  ❌  |
| Admin users CRUD          |  ✅  |  ❌  |  ❌  |
| Audit log                 |  ✅  |  ❌  |  ❌  |

Sidebar partial hides links the current user can't reach.

### Audit filter

Global MVC action filter, fires after every `POST/PUT/DELETE/PATCH` action. Writes one `AdminAuditLog` row via `IAdminAuditLogger`. Reads route values for the entity id; serializes context to JSON in `Details`.

### Security hardening

- `[AutoValidateAntiforgeryToken]` global filter.
- `UseHsts()` + `UseHttpsRedirection()`.
- `Cache-Control: no-store` on all authenticated responses.
- Per-IP login throttle: 10 attempts / 10 minutes via `IMemoryCache`.

## 7. Per-Module Page Breakdown

### Module 1 — Dashboard
- `GET /` → KPI cards (counts), 30-day shipments line chart, status pie chart, recent activity feed.
- `GET /Dashboard/LiveMap` → Leaflet map; SignalR client subscribed to `/hubs/shipment-tracking` on the WebApi.

### Module 2 — Users
- `GET /Merchants`, `GET /Drivers`, `GET /TruckAccounts` → filterable, paginated DataTables.
- `GET /<area>/Details/{id}` → profile + related data.
- `POST /<area>/Block/{id}`, `POST /<area>/Unblock/{id}`, `POST /<area>/Verify/{id}`.

### Module 3 — Trucks
- `GET /Trucks?status=Pending|Approved|Rejected|All`.
- `GET /Trucks/Details/{id}` → photos, owner, driver, shipment history.
- `POST /Trucks/Approve/{id}`, `POST /Trucks/Reject/{id}` (reason).
- `POST /Trucks/Suspend/{id}`.

### Module 4 — Shipments
- `GET /Shipments` → filters: status, date range, merchant, driver, governorate.
- `GET /Shipments/Details/{id}` → full timeline, route (Leaflet), offers, driver/truck info.
- `POST /Shipments/Cancel/{id}` (reason).
- `POST /Shipments/Reassign/{id}` (new driver).
- `GET /ShipmentRequests` → open requests + offers.

### Module 5 — Invitations
- `GET /Invitations?status=Pending|Accepted|Expired`.
- `POST /Invitations/Resend/{id}`, `POST /Invitations/Cancel/{id}`.

### Module 6 — Geography
- `GET/POST /Geography/Governorates` (CRUD).
- `GET/POST /Geography/Regions` (CRUD, scoped by governorate).
- `POST /Geography/Import` (CSV upload).

### Module 7 — Notifications
- `GET /Notifications/Compose` — target: all / by user type / single user.
- `POST /Notifications/Send`.
- `GET /Notifications/History` — delivery results, failed-delivery filter.
- `GET /Notifications/Devices` — FCM tokens registry.

### Module 8 — Reports
- `GET /Reports/Shipments?from=&to=&format=xlsx|csv` → file download via `IExcelExportService` (ClosedXML).
- `GET /Reports/Users`, `GET /Reports/Performance/Drivers`, `GET /Reports/Performance/Merchants`.

### Module 9 — Settings
- `GET/POST /Settings/App` — typed editor over `AppSettings` key/value.
- `GET /Settings/Admins`, `POST /Settings/Admins/Create`, `POST /Settings/Admins/SetRole/{id}`, `POST /Settings/Admins/Reset/{id}`, `POST /Settings/Admins/Delete/{id}`.

### Module 10 — Audit & Logs
- `GET /Audit` → filterable `AdminAuditLog` table (admin, action, entity type, date).
- `GET /Audit/LoginAttempts` → `AdminLoginAttempt` table.

## 8. Error Handling

- Global MVC exception filter renders `Views/Shared/Error.cshtml` with a request id; logs via `ILogger`.
- Friendly status code pages via `UseStatusCodePagesWithReExecute("/Error/{0}")`:
  - `403` → AccessDenied view (link back to dashboard).
  - `404` → NotFound view.
  - `500` → generic error view.
- Validation errors → `ModelState` + Bootstrap-styled validation summaries / inline messages via tag helpers.
- Flash messages → `TempData["Success"]` / `TempData["Error"]` rendered by `_Layout.cshtml`.
- Confirmation dialogs for destructive actions via **SweetAlert2** (block, cancel, delete, reject).

## 9. Testing & Verification

No test project exists in the solution today; adding one is out of scope for this work. Verification approach:

- **Manual smoke checklist** committed alongside the implementation plan, covering each module's happy path.
- **Build verification**: `dotnet build` must pass for the whole solution.
- **Migration verification**: `dotnet ef migrations script` reviewed before apply; `db.Database.MigrateAsync()` must run cleanly on a clean dev database.
- **Auth verification**: cannot reach any non-`/Account/*` page without a valid cookie; ReadOnly cannot POST; SuperAdmin can manage admins.
- **Deploy verification**: existing `scripts/deploy.sh` updated; health-check curl against the admin port returns `200` after restart.

## 10. Deployment Changes

- `HM.AdminPanel.csproj` added to `dotnet publish` step in `scripts/deploy.sh`.
- New systemd unit `hm-admin.service` on the prod host, listening on port `5050`.
- Reverse-proxy entry (nginx) for `admin.hm.fustani.cloud` → `127.0.0.1:5050`.
- `appsettings.json` for AdminPanel preserved across deploys (same pattern as WebApi).
- Migration runs from `Hm.WebApi` startup (already in place); AdminPanel does NOT run migrations to avoid races.

## 11. Open Questions / TBD

- Admin URL: subdomain (`admin.hm.fustani.cloud`) vs path (`/admin`). Default plan: subdomain.
- Initial SuperAdmin email/password — configured in `appsettings.json` on the prod box on first deploy.

## 12. Out of Scope

- Adding a test project.
- 2FA for admin login (can be a follow-up).
- Localization (English only for v1+v2).
- Mobile-friendly admin UI (desktop-first; AdminLTE is responsive by default but not optimized).
- Self-service admin password change UI (SuperAdmin resets via Module 9 instead) — can be added later.
