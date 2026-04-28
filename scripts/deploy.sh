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
trap cleanup EXIT

# Acquire exclusive lock (non-blocking: fail fast if another deploy is running).
exec 9>"$LOCK_FILE"
flock -n 9 || fail "another deploy is already in progress"

log "lock acquired; staging dir: $STAGING_DIR"

# 1. Sync source from origin/main.
log "syncing source from origin/main"
cd "$SOURCE_DIR"
git fetch origin
git checkout main
git reset --hard origin/main
log "source at commit: $(git rev-parse --short HEAD)"

# 2. Build (publish) into staging dir.
log "publishing to $STAGING_DIR"
dotnet publish "$SOURCE_DIR/Hm.WebApi/Hm.WebApi.csproj" \
  -c Release \
  -o "$STAGING_DIR" \
  --nologo \
  -v minimal
log "build OK; $(find "$STAGING_DIR" -maxdepth 1 -type f | wc -l) files in staging"

# 3. Snapshot current deployment so we can roll back.
log "backing up current $DEPLOY_DIR to $BACKUP_DIR"
rm -rf "$BACKUP_DIR"
cp -a "$DEPLOY_DIR" "$BACKUP_DIR"

# 4. Stop service before touching DB or binaries.
log "stopping $SERVICE_NAME"
systemctl stop "$SERVICE_NAME"

# 5. Read production connection string from the (preserved) prod appsettings.
log "reading production connection string"
PROD_CONN="$(jq -r '.ConnectionStrings.DefaultConnection' "$DEPLOY_DIR/appsettings.json")"
if [[ -z "$PROD_CONN" || "$PROD_CONN" == "null" ]]; then
  systemctl start "$SERVICE_NAME" || true
  fail "could not read ConnectionStrings.DefaultConnection from $DEPLOY_DIR/appsettings.json"
fi

# 6. Apply EF migrations.
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

# 7. Swap binaries — service is already stopped; explicitly preserve prod-only files.
log "syncing staging -> $DEPLOY_DIR"
rsync -a --delete \
  --exclude='appsettings.json' \
  --exclude='uploads' \
  --exclude='Secrets' \
  "$STAGING_DIR/" "$DEPLOY_DIR/"
log "swap complete"

# --- Steps will be appended in subsequent tasks ---

log "deploy complete"
