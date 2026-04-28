#!/usr/bin/env bash
# HM auto-deploy. Runs on the production server. Single source of truth.
# See docs/superpowers/specs/2026-04-28-auto-deploy-design.md
set -euo pipefail

readonly LOCK_FILE="/var/run/hm-deploy.lock"
readonly SOURCE_DIR="/opt/hm-source"
readonly DEPLOY_DIR="/var/www/hm"
readonly BACKUP_DIR="/var/www/hm.prev"
readonly STAGING_DIR="$(mktemp -d -t hm-publish-XXXXXX)"
readonly SERVICE_NAME="hm"
readonly HEALTH_URL="https://hm.fustani.cloud/swagger/index.html"
export PATH="$PATH:/root/.dotnet/tools"

log()  { printf '\033[0;36m[deploy]\033[0m %s\n' "$*"; }
fail() { printf '\033[0;31m[deploy:FAIL]\033[0m %s\n' "$*" >&2; exit 1; }

cleanup() {
  rm -rf "$STAGING_DIR"
}

# Restores /var/www/hm from /var/www/hm.prev and restarts the service.
# Does NOT exit; caller must call fail() afterward.
rollback() {
  log "ROLLBACK: restoring previous binaries"
  systemctl stop "$SERVICE_NAME" 2>/dev/null || true
  if [[ -d "$BACKUP_DIR" ]]; then
    rm -rf "$DEPLOY_DIR"
    mv "$BACKUP_DIR" "$DEPLOY_DIR"
  fi
  systemctl start "$SERVICE_NAME" || true
  log "ROLLBACK: last 50 lines of journalctl -u $SERVICE_NAME:"
  journalctl -u "$SERVICE_NAME" -n 50 --no-pager || true
}

trap cleanup EXIT

# 1. Acquire exclusive lock (non-blocking: fail fast if another deploy is running).
exec 9>"$LOCK_FILE"
flock -n 9 || fail "another deploy is already in progress"

log "lock acquired; staging dir: $STAGING_DIR"

# 2. Sync source from origin/main.
log "syncing source from origin/main"
cd "$SOURCE_DIR"
git fetch origin
git checkout main
git reset --hard origin/main
log "source at commit: $(git rev-parse --short HEAD)"

# 3. Build (publish) into staging dir.
log "publishing to $STAGING_DIR"
dotnet publish "$SOURCE_DIR/Hm.WebApi/Hm.WebApi.csproj" \
  -c Release \
  -o "$STAGING_DIR" \
  --nologo \
  -v minimal
log "build OK; $(find "$STAGING_DIR" -maxdepth 1 -type f | wc -l) files in staging"

# 4. Read production connection string from the (preserved) prod appsettings.
# Reading before stop/backup so a misconfigured appsettings fails fast with no state change.
log "reading production connection string"
PROD_CONN="$(jq -r '.ConnectionStrings.DefaultConnection' "$DEPLOY_DIR/appsettings.json")"
if [[ -z "$PROD_CONN" || "$PROD_CONN" == "null" ]]; then
  fail "could not read ConnectionStrings.DefaultConnection from $DEPLOY_DIR/appsettings.json"
fi

# 5. Snapshot current deployment so we can roll back.
# NOTE: this copies uploads/ which may be large and is excluded from the rsync swap.
# uploads/ is preserved in-place across deploys; the backup copy exists only so
# rollback can restore the entire /var/www/hm tree atomically if needed.
log "backing up current $DEPLOY_DIR to $BACKUP_DIR"
rm -rf "$BACKUP_DIR"
cp -a "$DEPLOY_DIR" "$BACKUP_DIR"

# 6. Stop service before touching DB or binaries.
log "stopping $SERVICE_NAME"
systemctl stop "$SERVICE_NAME"

# 7. Apply EF migrations.
# --no-build is safe because step 3 already built Release artifacts in $STAGING_DIR
# and the source-tree bin/ for HM.Infrastructure + Hm.WebApi (dotnet publish writes both).
# If EF errors with "could not find assembly", drop --no-build to let it rebuild.
log "applying database migrations"
if ! dotnet ef database update \
       --project "$SOURCE_DIR/HM.Infrastructure/HM.Infrastructure.csproj" \
       --startup-project "$SOURCE_DIR/Hm.WebApi/Hm.WebApi.csproj" \
       --configuration Release \
       --no-build \
       --connection "$PROD_CONN"; then
  log "migration failed; restarting service from old binaries (swap not yet performed)"
  systemctl start "$SERVICE_NAME" || true
  fail "migration failed"
fi
log "migrations applied"

# 8. Swap binaries — service is already stopped; explicitly preserve prod-only files.
log "syncing staging -> $DEPLOY_DIR"
rsync -a --delete \
  --exclude='appsettings.json' \
  --exclude='uploads' \
  --exclude='Secrets' \
  "$STAGING_DIR/" "$DEPLOY_DIR/"
log "swap complete"

# 9. Start service.
log "starting $SERVICE_NAME"
systemctl start "$SERVICE_NAME"

# 10. Verify systemd reports active within 30s, then verify Swagger health.
log "waiting for service to become active"
for i in {1..30}; do
  if systemctl is-active --quiet "$SERVICE_NAME"; then
    log "service active after ${i}s"
    break
  fi
  if [[ $i -eq 30 ]]; then
    rollback
    fail "service did not become active within 30s"
  fi
  sleep 1
done

log "verifying $HEALTH_URL"
# 30s max + 3 retries with 5s delay tolerates JIT-cold ASP.NET startup
# (loading EF context, SignalR hubs, Firebase SDK).
if ! curl -fsS --max-time 30 --retry 3 --retry-delay 5 "$HEALTH_URL" -o /dev/null; then
  rollback
  fail "Swagger health-check failed at $HEALTH_URL"
fi
log "health-check OK"

log "deploy complete"
