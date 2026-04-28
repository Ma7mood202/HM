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

# --- Steps will be appended in subsequent tasks ---

log "deploy complete"
