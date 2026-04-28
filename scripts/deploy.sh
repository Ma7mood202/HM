#!/usr/bin/env bash
# HM auto-deploy. Runs on the production server. Single source of truth.
# See docs/superpowers/specs/2026-04-28-auto-deploy-design.md
set -euo pipefail

readonly LOCK_FILE="/var/run/hm-deploy.lock"

# 1. Acquire exclusive lock by re-exec'ing under flock(1).
# flock(1) opens the lock file with close-on-exec, so dotnet's MSBuild "node reuse"
# build-server children don't inherit the fd and survive past the script. The
# in-shell "exec 9>; flock 9" pattern leaks the fd to dotnet children (which
# linger 12+ hours), permanently holding the lock.
if [[ "${HM_DEPLOY_FLOCKED:-}" != "1" ]]; then
  export HM_DEPLOY_FLOCKED=1
  exec flock -n "$LOCK_FILE" "$0" "$@"
  # If exec returns, flock could not be exec'd; if flock could not acquire the lock,
  # it exits 1 and we never reach this line. Either way, propagate.
  echo "[deploy:FAIL] could not acquire deploy lock at $LOCK_FILE" >&2
  exit 1
fi

# Below this line we are running with the lock held.
readonly SOURCE_DIR="/opt/hm-source"
readonly DEPLOY_DIR="/var/www/hm"
readonly BACKUP_DIR="/var/www/hm.prev"
readonly STAGING_DIR="$(mktemp -d -t hm-publish-XXXXXX)"
# mktemp creates with mode 700, but rsync -a propagates that to $DEPLOY_DIR,
# breaking chdir for the service user (www-data). Force traversable mode.
chmod 755 "$STAGING_DIR"
readonly SERVICE_NAME="hm"
readonly HEALTH_URL="https://hm.fustani.cloud/swagger/index.html"
export PATH="$PATH:/root/.dotnet/tools"

# Disable MSBuild "node reuse" build-server children. They linger 12+ hours
# and inherit the flock lock fd, blocking subsequent deploys until we manually
# kill them. Cleaner to not spawn them in the first place.
export MSBUILDDISABLENODEREUSE=1
export DOTNET_BUILD_SERVER=0

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
  -v minimal \
  /nodeReuse:false
log "build OK; $(find "$STAGING_DIR" -maxdepth 1 -type f | wc -l) files in staging"

# 4. Read production connection string from the (preserved) prod appsettings.
# Reading before stop/backup so a misconfigured appsettings fails fast with no state change.
# .NET appsettings.json is JSONC (allows // and /* */ comments and trailing commas);
# stdlib jq rejects those, so we use Python with a minimal JSONC sanitizer.
log "reading production connection string"
PROD_CONN="$(python3 - "$DEPLOY_DIR/appsettings.json" <<'PYEOF'
import json, re, sys
with open(sys.argv[1]) as f:
    content = f.read()
content = re.sub(r'(?m)^\s*//.*$', '', content)        # strip whole-line // comments
content = re.sub(r'/\*.*?\*/', '', content, flags=re.DOTALL)  # strip /* */ blocks
content = re.sub(r',(\s*[}\]])', r'\1', content)       # strip trailing commas
data = json.loads(content)
print(data['ConnectionStrings']['DefaultConnection'])
PYEOF
)" || fail "could not parse $DEPLOY_DIR/appsettings.json"
if [[ -z "$PROD_CONN" || "$PROD_CONN" == "None" ]]; then
  fail "ConnectionStrings.DefaultConnection is empty in $DEPLOY_DIR/appsettings.json"
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
# Ensure the deploy dir itself is traversable by the systemd service user (www-data).
chmod 755 "$DEPLOY_DIR"
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
