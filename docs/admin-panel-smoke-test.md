# HM Admin Panel v1 — smoke test

Run after every release. Use seeded SuperAdmin and a separate Support account
(seed a second admin manually via DB once roles are seeded).

## Auth

- [ ] `/` redirects to `/Account/Login` when unauthenticated
- [ ] Invalid credentials show "Invalid credentials" error
- [ ] An `AdminLoginAttempts` row exists with `Success = false` for every bad attempt
- [ ] 10 invalid attempts in 10 min produce "Too many failed attempts" message
- [ ] Valid SuperAdmin login redirects to `/Dashboard`
- [ ] An `AdminLoginAttempts` row exists with `Success = true` for the good login
- [ ] Logout returns to login, cookie cleared
- [ ] Direct GET of `/Merchants` while logged out → redirected to `/Account/Login?returnUrl=/Merchants`

## Dashboard

- [ ] KPI cards show counts (will be 0s on a fresh DB)
- [ ] Line chart renders 30 days (mostly empty bars OK)
- [ ] Doughnut chart renders status breakdown
- [ ] Recent activity table loads (may be empty)
- [ ] `/Dashboard/LiveMap` opens, OSM tiles load
- [ ] If WebApi is running, SignalR connection opens (check browser console for "Connected")

## Merchants / Drivers / TruckAccounts (Phase F)

For each of /Merchants, /Drivers, /TruckAccounts:

- [ ] Index lists rows, search by name/phone/email works
- [ ] Pager works (Prev/Next move between pages when there are enough rows)
- [ ] Details opens via "Open" button
- [ ] Block requires a reason, succeeds, SweetAlert confirm appears
- [ ] An `AdminAuditLogs` row is written for the Block action (controller + action + entity id)
- [ ] Unblock succeeds; `IsBlocked = false`, `BlockedAt` cleared
- [ ] Verify activates user (`IsActive = true`, `IsOtpVerified = true`)

## Trucks (Phase G)

- [ ] Filter by `Pending` shows only pending trucks
- [ ] Approve flips `ApprovalStatus = Approved` and clears `RejectionReason`
- [ ] Reject with reason saves both status and reason
- [ ] Suspend flips `IsActive = false`
- [ ] Approve/Reject/Suspend each produce an `AdminAuditLogs` row

## Shipments (Phase H)

- [ ] Filter by status works
- [ ] Filter by date range works
- [ ] Details shows map if `CurrentLat`/`CurrentLng` are present
- [ ] Cancel sets `Status = Cancelled` and stamps `CompletedAt`
- [ ] Reassign updates `DriverProfileId` and `AssignedAt`
- [ ] `/ShipmentRequests` lists the 200 most recent requests with offers count

## Authorization

- [ ] ReadOnly account: GET pages work; POST actions return 403 (AccessDenied page)
- [ ] Support account: GET + POST work; cannot reach `/Settings/Admins` (v2 module — should 403 once added)
- [ ] SuperAdmin: everything works

## Migration & seeding

- [ ] Fresh DB: `db.Database.MigrateAsync()` runs cleanly, all three new tables exist
- [ ] All 7 roles (`Merchant`, `TruckAccount`, `Driver`, `Admin`, `SuperAdmin`, `Support`, `ReadOnly`) exist in `AspNetRoles`
- [ ] The seeded SuperAdmin user exists in `AspNetUsers` with email from `AdminPanel:SeedAdmin:Email`
- [ ] That user has both `Admin` and `SuperAdmin` role entries in `AspNetUserRoles`

## Deployment

- [ ] `scripts/deploy.sh` runs cleanly through Step 11 (HM.AdminPanel publish)
- [ ] `systemctl is-active hm-admin` reports `active`
- [ ] `curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:5050/Account/Login` returns `200`
- [ ] `https://admin.hm.fustani.cloud/Account/Login` returns the login page in a browser
