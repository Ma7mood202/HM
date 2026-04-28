# Auto-Deploy to Production Server — Design

**Date:** 2026-04-28
**Status:** Approved (pending written-spec review)
**Repo:** `https://github.com/Ma7mood202/HM.git`
**Target server:** `root@72.60.134.91` (https://hm.fustani.cloud)

## Goal

Give Claude a one-command way to publish the latest version of the HM Web API to the production server, run with a "build + verify, then ask before pushing" workflow. The deploy must preserve production-only files (config, uploads, secrets), run database migrations, verify health, and auto-roll-back on failure.

## Decisions (locked in during brainstorming)

| Topic | Choice |
|---|---|
| Trigger | Build/verify locally → ask user → run on confirmation |
| Build location | On the server (server already has .NET 8) |
| Auth to server | New SSH key (`~/.ssh/hm_deploy`); existing root password preserved for human use |
| Source path on server | `/opt/hm-source` (fresh clone, never touched manually) |
| Branch policy | Deploy `main` only; Claude merges feature branch → main → push automatically |
| GitHub auth (server) | Read-only deploy key on the GitHub repo |
| Trigger UI | `/deploy` slash command wrapping `scripts/deploy.sh` |
| Health check | `curl -fsS https://hm.fustani.cloud/swagger/index.html` returns 200 |
| Database migrations | `dotnet ef database update` runs on every deploy |
| Concurrency | `flock` on `/var/run/hm-deploy.lock` |

## Architecture

### One-time setup (run once, by hand or guided)

1. **Local SSH key.** Generate `~/.ssh/hm_deploy` (ed25519). Add a host alias `hm` in `~/.ssh/config` pointing at `root@72.60.134.91` with `IdentityFile ~/.ssh/hm_deploy`.
2. **Authorize on server.** User pastes a one-liner into their existing terminal session that appends the public key to `/root/.ssh/authorized_keys`. Password authentication remains enabled.
3. **Server-side GitHub deploy key.** SSH to server, generate `~/.ssh/hm_github_deploy` (ed25519). User adds the public key as a read-only deploy key on the GitHub repo.
4. **Clone source.** `git clone git@github.com:Ma7mood202/HM.git /opt/hm-source` (using the deploy key).
5. **Verify environment.** `dotnet --version` returns 8.x; `which rsync git flock jq` succeeds; `dotnet tool install --global dotnet-ef` if `dotnet ef` not present.
6. **nginx WebSocket check.** Grep `/etc/nginx/sites-available/hm` for `proxy_set_header Upgrade` and `Connection "upgrade"` on the SignalR location. Report and remediate if missing.

### Per-deploy flow (`scripts/deploy.sh`, server-side)

Order of operations, top to bottom. Any non-zero step aborts the deploy with rollback if `/var/www/hm.prev` exists.

1. **Acquire lock.** `flock -n /var/run/hm-deploy.lock` — fail fast if another deploy is in flight.
2. **Sync source.**
   ```
   cd /opt/hm-source
   git fetch origin
   git checkout main
   git reset --hard origin/main
   ```
3. **Build.** `dotnet publish Hm.WebApi/Hm.WebApi.csproj -c Release -o /tmp/hm-publish-$$/`. Build failure → abort, service untouched.
4. **Read production connection string.** `jq -r '.ConnectionStrings.DefaultConnection' /var/www/hm/appsettings.json` — needed because the source-tree `appsettings.json` is dev config.
5. **Backup current deployment.** `rm -rf /var/www/hm.prev && cp -a /var/www/hm /var/www/hm.prev`.
6. **Stop service.** `systemctl stop hm`.
7. **Run migrations.**
   ```
   dotnet ef database update \
     --project HM.Infrastructure \
     --startup-project Hm.WebApi \
     --connection "$PROD_CONN_STR"
   ```
   Migration failure → restart service from old binaries (`systemctl start hm`), report, exit. No file swap occurred so no rollback needed beyond restart.
8. **Swap binaries.** `rsync -a --delete --exclude=appsettings.json --exclude=uploads --exclude=Secrets /tmp/hm-publish-$$/ /var/www/hm/`.
9. **Start service.** `systemctl start hm`.
10. **Verify.**
    - Wait up to 30s for `systemctl is-active hm` → `active`.
    - `curl -fsS https://hm.fustani.cloud/swagger/index.html` returns 200.
    - On either failure: auto-rollback (stop, replace `/var/www/hm` from `/var/www/hm.prev`, restart). Report failure with last 50 lines of `journalctl -u hm`.
11. **Cleanup.** `rm -rf /tmp/hm-publish-$$/`. Release lock.

### Per-deploy flow (Claude side, before invoking the script)

1. Confirm working tree clean and current branch pushed.
2. If current branch ≠ `main`: fast-forward `main` to current branch, push `main`. (Pause and ask if FF is impossible.)
3. Local pre-flight: `dotnet build -c Release` against `main`. (Note: requires .NET 8 SDK locally — see "Open issue" below.)
4. Tell user: *"Build is clean on main. Deploy now? [yes/no]"*
5. On yes → `ssh hm 'bash /opt/hm-source/scripts/deploy.sh'` → stream output → report verification outcome.

## Files Created

- `scripts/deploy.sh` — the entire server-side deploy. Idempotent, single source of truth.
- `docs/superpowers/specs/2026-04-28-auto-deploy-design.md` — this file.
- `~/.ssh/hm_deploy` (and `.pub`) — local-only, gitignored by default since it's outside the repo.
- `~/.ssh/config` entry — `Host hm` alias, local-only.
- (Server) `/opt/hm-source/` — repo clone.
- (Server) `~/.ssh/hm_github_deploy` — deploy key for GitHub.
- (Server) `/var/run/hm-deploy.lock` — created at first deploy.

## Files Preserved Across Deploys

Listed by name in the rsync exclude list — never implicit:

- `/var/www/hm/appsettings.json`
- `/var/www/hm/uploads/` (entire tree)
- `/var/www/hm/Secrets/` (entire tree, includes `firebase-service-account.json`)

## Verified Assumptions (during spec review)

- DB stack is PostgreSQL via Npgsql (`HM.Infrastructure/DependencyInjection.cs`).
- Connection string key is `ConnectionStrings.DefaultConnection` in `appsettings.json`.
- Migrations live in `HM.Infrastructure/Migrations/`; `dotnet ef --project HM.Infrastructure --startup-project Hm.WebApi` is the correct invocation.
- Server-side `dotnet --version` will need to satisfy `global.json` (`8.0.413`); if the installed SDK is a different 8.x patch, either install 8.0.413 on the server or relax `global.json` to allow `rollForward`.

## Open issue

**Local pre-flight build needs .NET 8 SDK.** This machine has SDKs 9 and 10 only; `global.json` pins 8.0.413. Two ways to handle:

- **(a) Skip the local pre-flight.** Server build is the only build; if it fails, the deploy aborts before stopping the service. Slight risk of "deploy starts, fails on the server, no harm done but cycle wasted."
- **(b) Install .NET 8 SDK locally** so we catch build errors before SSHing.

Recommend **(a)** for now (server build is already the gate), revisit if server build failures become common.

## Out of Scope

- CI/CD via GitHub Actions.
- Automated triggering on commit (explicitly rejected — too aggressive for prod).
- Blue/green or zero-downtime deploys (`stop → swap → start` is acceptable; downtime is seconds).
- Automatic nginx config edits (we verify, not modify).
- `appsettings.json` migration assistance (any new config keys must be added manually on the server).

## Failure Modes & Responses

| Failure point | Behavior |
|---|---|
| `git fetch` / clone fails | Abort, no service impact, surface git error. |
| `dotnet publish` fails | Abort, service still running, surface compiler error. |
| Migration fails | Restart service from old binaries (no swap occurred), surface migration error. |
| File swap fails midway | Service is stopped; auto-restore from `/var/www/hm.prev`, restart, report. |
| Service fails to start | Auto-restore, restart, report `journalctl` tail. |
| Swagger health-check fails | Auto-restore, restart, report. |
| Concurrent deploy attempt | `flock` rejects, prints "deploy already in progress", exits non-zero. |
