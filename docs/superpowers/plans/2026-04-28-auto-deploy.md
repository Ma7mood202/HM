# Auto-Deploy to Production Server Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a one-command deploy flow that publishes the HM Web API to `https://hm.fustani.cloud` from `main` only, with backup, EF migrations, post-deploy Swagger health-check, and auto-rollback.

**Architecture:** Server-side build (server has .NET 8). Single `scripts/deploy.sh` is the only deploy logic, committed to the repo. A `/deploy` slash command wraps it with `ssh hm 'cd /opt/hm-source && git fetch && git reset --hard origin/main && bash scripts/deploy.sh'` so each deploy runs the latest version of the script. SSH key auth (root password preserved). Production-only files (`appsettings.json`, `uploads/`, `Secrets/`) are explicitly excluded from rsync. flock prevents concurrent deploys.

**Tech Stack:** Bash, ssh/scp, rsync, systemd, dotnet 8 SDK, dotnet-ef, jq, flock, curl, nginx, PostgreSQL.

---

## File Structure

| Path | Created/Modified | Responsibility |
|---|---|---|
| `scripts/deploy.sh` | Create | Server-side deploy: sync source → build → migrate → backup → swap → start → verify → rollback-on-fail |
| `.claude/commands/deploy.md` | Create | `/deploy` slash command — instructs me to merge to main, push, then SSH-invoke `deploy.sh` |
| `docs/superpowers/specs/2026-04-28-auto-deploy-design.md` | Already exists | Reference design document |
| `~/.ssh/hm_deploy` (local) | Create | Private key for SSH to server. Outside repo. |
| `~/.ssh/config` (local) | Modify | Add `Host hm` alias entry |
| `/opt/hm-source/` (server) | Create | Repo clone for builds |
| `~/.ssh/hm_github_deploy` (server) | Create | Private key for GitHub deploy access |
| `/var/run/hm-deploy.lock` (server) | Created at first run | flock lock file |

---

## Note on Verification

Shell-script tasks aren't unit-tested; they're verified by running them and inspecting effects. Each task ends with a concrete verification command and its expected output. Where a task could leave the server in a bad state, the verification is constructed to be safe (dry-run, idempotent, or read-only).

---

## Task 1: One-time setup — Local SSH key + config

**Files:**
- Create: `~/.ssh/hm_deploy` (private key, local)
- Create: `~/.ssh/hm_deploy.pub` (public key, local)
- Modify: `~/.ssh/config` (local, append-only)

- [ ] **Step 1: Generate SSH keypair**

Run:
```bash
ssh-keygen -t ed25519 -f ~/.ssh/hm_deploy -N "" -C "claude-hm-deploy"
```
Expected: prints `Your identification has been saved in /c/Users/.../hm_deploy` and the SHA256 fingerprint.

- [ ] **Step 2: Append SSH config entry**

Run:
```bash
cat >> ~/.ssh/config << 'EOF'

Host hm
    HostName 72.60.134.91
    User root
    IdentityFile ~/.ssh/hm_deploy
    IdentitiesOnly yes
EOF
```
Expected: no output. (`IdentitiesOnly yes` prevents ssh-agent from offering other keys.)

- [ ] **Step 3: Print public key for the user to authorize**

Run:
```bash
cat ~/.ssh/hm_deploy.pub
```
Expected: a single line beginning `ssh-ed25519 AAAA...` ending `claude-hm-deploy`.

- [ ] **Step 4: Hand the user the one-line command to authorize on server**

Tell the user (replace `<PUBKEY>` with the actual line from Step 3):
```
SSH to your server with your usual root password and run this single command:

  mkdir -p ~/.ssh && chmod 700 ~/.ssh && echo '<PUBKEY>' >> ~/.ssh/authorized_keys && chmod 600 ~/.ssh/authorized_keys

Reply 'done' when authorized.
```

- [ ] **Step 5: Verify key-based login works**

After user confirms, run:
```bash
ssh -o BatchMode=yes hm 'echo OK && hostname'
```
Expected: prints `OK` followed by the server hostname. `BatchMode=yes` ensures it fails immediately if key auth doesn't work, rather than prompting for a password.

- [ ] **Step 6: Commit SSH config note (no secrets) — skipped**

No commit yet — local SSH artifacts aren't in the repo.

---

## Task 2: One-time setup — Server-side GitHub deploy key

**Files:**
- Create: `/root/.ssh/hm_github_deploy` (server)
- Create: `/root/.ssh/hm_github_deploy.pub` (server)
- Modify: `/root/.ssh/config` (server, append-only)

- [ ] **Step 1: Generate GitHub deploy key on the server**

Run:
```bash
ssh hm 'ssh-keygen -t ed25519 -f ~/.ssh/hm_github_deploy -N "" -C "hm-server-github"'
```
Expected: confirmation that the key was saved.

- [ ] **Step 2: Add SSH config entry for GitHub on the server**

Run:
```bash
ssh hm "cat >> ~/.ssh/config << 'EOF'

Host github.com
    HostName github.com
    User git
    IdentityFile ~/.ssh/hm_github_deploy
    IdentitiesOnly yes
EOF"
```
Expected: no output.

- [ ] **Step 3: Print the GitHub deploy public key**

Run:
```bash
ssh hm 'cat ~/.ssh/hm_github_deploy.pub'
```
Expected: a single line beginning `ssh-ed25519 AAAA...` ending `hm-server-github`.

- [ ] **Step 4: Hand the user the GitHub deploy-key instructions**

Tell the user:
```
Add this as a deploy key on the GitHub repo:

1. Open https://github.com/Ma7mood202/HM/settings/keys
2. Click "Add deploy key"
3. Title: "hm-server"
4. Key: <paste the public key from Step 3>
5. LEAVE "Allow write access" UNCHECKED (read-only)
6. Click "Add key"

Reply 'added' when done.
```

- [ ] **Step 5: Verify GitHub access from server**

After user confirms, run:
```bash
ssh hm 'ssh -o StrictHostKeyChecking=accept-new -T git@github.com 2>&1 | grep -E "successfully authenticated|denied"'
```
Expected: `Hi Ma7mood202/HM! You've successfully authenticated, but GitHub does not provide shell access.`

---

## Task 3: One-time setup — Clone repo and verify build environment on server

**Files:**
- Create: `/opt/hm-source/` (server, full repo clone)

- [ ] **Step 1: Clone the repo to /opt/hm-source**

Run:
```bash
ssh hm 'git clone git@github.com:Ma7mood202/HM.git /opt/hm-source'
```
Expected: `Cloning into '/opt/hm-source'...` followed by progress, then `done.`.

- [ ] **Step 2: Verify .NET SDK and required tools on server**

Run:
```bash
ssh hm 'dotnet --version && which rsync git flock jq curl'
```
Expected: prints an 8.x version on the first line, then absolute paths for each tool. If `dotnet --version` errors saying SDK 8.0.413 not found, run:
```bash
ssh hm 'dotnet --list-sdks'
```
to see which 8.x patch is available, then proceed to Step 3 to handle this.

- [ ] **Step 3: Resolve global.json SDK pin if needed**

If Step 2 showed a different 8.x patch (e.g. 8.0.404), patch `global.json` to allow `rollForward`:

In the **local repo**, edit [global.json](global.json) so it reads:
```json
{
  "sdk": {
    "version": "8.0.413",
    "rollForward": "latestPatch"
  }
}
```
Then commit:
```bash
git add global.json
git commit -m "chore: allow latestPatch rollForward on net8 SDK"
```

If Step 2's `dotnet --version` already returned `8.0.413`, skip this step.

- [ ] **Step 4: Install dotnet-ef tool on server**

Run:
```bash
ssh hm 'dotnet tool install --global dotnet-ef --version 8.0.* || dotnet tool update --global dotnet-ef --version 8.0.*'
```
Expected: either `Tool 'dotnet-ef' was successfully installed` or `Tool 'dotnet-ef' was successfully updated`.

- [ ] **Step 5: Add ~/.dotnet/tools to PATH for root's non-interactive shells**

Run:
```bash
ssh hm 'grep -q "/.dotnet/tools" ~/.bashrc || echo "export PATH=\$PATH:/root/.dotnet/tools" >> ~/.bashrc'
```
Expected: no output. (Idempotent.)

- [ ] **Step 6: Verify dotnet-ef is callable from a non-interactive ssh**

Run:
```bash
ssh hm 'PATH=$PATH:/root/.dotnet/tools dotnet ef --version'
```
Expected: a version string starting with `8.`.

- [ ] **Step 7: Verify nginx WebSocket headers**

Run:
```bash
ssh hm 'grep -E "proxy_set_header (Upgrade|Connection)" /etc/nginx/sites-available/hm'
```
Expected: at least two lines —
```
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
```
(or equivalent). If missing entirely, **stop and tell the user** — fixing the nginx config is a separate decision before deploys can validate SignalR end-to-end.

---

## Task 4: Create the deploy script — skeleton with lock + cleanup

**Files:**
- Create: `scripts/deploy.sh`

- [ ] **Step 1: Create the script with shebang, strict mode, lock, and cleanup trap**

Create `scripts/deploy.sh`:
```bash
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
readonly PATH="$PATH:/root/.dotnet/tools"

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

# --- Steps will be appended in subsequent tasks ---

log "deploy complete"
```

- [ ] **Step 2: Make it executable**

Run:
```bash
chmod +x scripts/deploy.sh
```

- [ ] **Step 3: Sanity-check syntax**

Run:
```bash
bash -n scripts/deploy.sh
```
Expected: no output (valid syntax).

- [ ] **Step 4: Commit**

```bash
git add scripts/deploy.sh
git commit -m "feat(deploy): add deploy script skeleton with lock and cleanup trap"
```

---

## Task 5: Add source-sync step

**Files:**
- Modify: `scripts/deploy.sh`

- [ ] **Step 1: Append the source-sync block before the final `log "deploy complete"`**

Edit `scripts/deploy.sh` — replace `# --- Steps will be appended in subsequent tasks ---` with:
```bash
# 1. Sync source from origin/main.
log "syncing source from origin/main"
cd "$SOURCE_DIR"
git fetch origin
git checkout main
git reset --hard origin/main
log "source at commit: $(git rev-parse --short HEAD)"

# --- Steps will be appended in subsequent tasks ---
```

- [ ] **Step 2: Sanity-check syntax**

Run: `bash -n scripts/deploy.sh`
Expected: no output.

- [ ] **Step 3: Commit & test on server**

```bash
git add scripts/deploy.sh
git commit -m "feat(deploy): sync source from origin/main"
git push origin <current-branch>
```

Then on the server, manually pull this branch and dry-run:
```bash
ssh hm "cd /opt/hm-source && git fetch && git checkout <current-branch> && bash scripts/deploy.sh"
```
Expected: prints `lock acquired`, `syncing source from origin/main`, `source at commit: <hash>`, then `deploy complete`. Server is unchanged otherwise.

(Note: on `main` only this will normally run; we test on the working branch by checking out the branch on the server first. After this task is done, the next task supersedes the source the script sees.)

---

## Task 6: Add build step

**Files:**
- Modify: `scripts/deploy.sh`

- [ ] **Step 1: Append the build block**

Replace the `# --- Steps will be appended in subsequent tasks ---` placeholder with:
```bash
# 2. Build (publish) into staging dir.
log "publishing to $STAGING_DIR"
dotnet publish "$SOURCE_DIR/Hm.WebApi/Hm.WebApi.csproj" \
  -c Release \
  -o "$STAGING_DIR" \
  --nologo \
  -v minimal
log "build OK; $(find "$STAGING_DIR" -maxdepth 1 -type f | wc -l) files in staging"

# --- Steps will be appended in subsequent tasks ---
```

- [ ] **Step 2: Sanity-check syntax**

Run: `bash -n scripts/deploy.sh`
Expected: no output.

- [ ] **Step 3: Commit & test build on server**

```bash
git add scripts/deploy.sh
git commit -m "feat(deploy): add dotnet publish step"
git push
```

Server test:
```bash
ssh hm "cd /opt/hm-source && git fetch && git checkout <current-branch> && bash scripts/deploy.sh"
```
Expected: source-sync logs, then `publishing to /tmp/hm-publish-XXXXX`, dotnet build output (under 60s), then `build OK; <N> files in staging`, then `deploy complete`. Service is still running, no files swapped.

---

## Task 7: Add backup + stop service + EF migration step

**Why this ordering:** the design spec requires migrations run while the service is stopped, so destructive schema changes (column drops, renames) never hit live traffic. We backup first so a failed migration can be followed by a clean restart of the old binaries (the swap hasn't happened yet).

**Files:**
- Modify: `scripts/deploy.sh`

- [ ] **Step 1: Append the backup + stop + migration block**

Replace the `# --- Steps will be appended in subsequent tasks ---` placeholder with:
```bash
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

# --- Steps will be appended in subsequent tasks ---
```

Note on `--no-build` + `--configuration Release`: we already published Release in Task 6, so EF reuses those artifacts. If EF errors with "could not find assembly", drop `--no-build` to let it rebuild.

- [ ] **Step 2: Sanity-check syntax**

Run: `bash -n scripts/deploy.sh`
Expected: no output.

- [ ] **Step 3: Commit**

```bash
git add scripts/deploy.sh
git commit -m "feat(deploy): backup + stop service + EF migrations (in that order)"
git push
```

(No standalone server test yet — this leaves the service stopped. We test the full flow once Task 9 starts the service back up and verifies.)

---

## Task 8: Add binary swap step

**Files:**
- Modify: `scripts/deploy.sh`

- [ ] **Step 1: Append the swap block**

Replace the `# --- Steps will be appended in subsequent tasks ---` placeholder with:
```bash
# 7. Swap binaries — service is already stopped; explicitly preserve prod-only files.
log "syncing staging -> $DEPLOY_DIR"
rsync -a --delete \
  --exclude='appsettings.json' \
  --exclude='uploads' \
  --exclude='Secrets' \
  "$STAGING_DIR/" "$DEPLOY_DIR/"
log "swap complete"

# --- Steps will be appended in subsequent tasks ---
```

- [ ] **Step 2: Sanity-check syntax**

Run: `bash -n scripts/deploy.sh`
Expected: no output.

- [ ] **Step 3: Commit**

```bash
git add scripts/deploy.sh
git commit -m "feat(deploy): rsync staging into deploy dir, preserving prod-only files"
git push
```

---

## Task 9: Add service start + verification + auto-rollback

**Files:**
- Modify: `scripts/deploy.sh`

- [ ] **Step 1: Define rollback function near the top**

Insert after the `cleanup()` function definition, **before** the `trap cleanup EXIT` line:
```bash
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
```

- [ ] **Step 2: Append the start + verify block**

Replace the final `# --- Steps will be appended in subsequent tasks ---` with:
```bash
# 7. Start service.
log "starting $SERVICE_NAME"
systemctl start "$SERVICE_NAME"

# 8. Verify systemd reports active within 30s.
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

# 9. Verify Swagger responds 200.
log "verifying $HEALTH_URL"
if ! curl -fsS --max-time 10 "$HEALTH_URL" -o /dev/null; then
  rollback
  fail "Swagger health-check failed at $HEALTH_URL"
fi
log "health-check OK"
```

- [ ] **Step 3: Sanity-check syntax**

Run: `bash -n scripts/deploy.sh`
Expected: no output.

- [ ] **Step 4: Commit**

```bash
git add scripts/deploy.sh
git commit -m "feat(deploy): start service, verify health, auto-rollback on failure"
git push
```

- [ ] **Step 5: Full end-to-end smoke test on server (still on working branch)**

This is the first real deploy. We do it manually on the working branch first, then once that passes, merge to main. **Do this only after the user confirms they're ready for a brief production downtime window (~10–30 seconds).**

Tell the user:
```
Ready to do a smoke-test deploy. The service will restart and be unavailable for 10-30 seconds. Confirm 'go' to proceed.
```

After 'go', run:
```bash
ssh hm "cd /opt/hm-source && git fetch && git checkout <current-branch> && bash scripts/deploy.sh"
```

Expected output sequence:
```
[deploy] lock acquired; staging dir: /tmp/hm-publish-XXXXX
[deploy] syncing source from origin/main             # (or current branch)
[deploy] source at commit: <hash>
[deploy] publishing to /tmp/hm-publish-XXXXX
[deploy] build OK; <N> files in staging
[deploy] reading production connection string
[deploy] applying database migrations
[deploy] migrations applied
[deploy] backing up current /var/www/hm to /var/www/hm.prev
[deploy] stopping hm
[deploy] syncing staging -> /var/www/hm
[deploy] swap complete
[deploy] starting hm
[deploy] waiting for service to become active
[deploy] service active after Ns
[deploy] verifying https://hm.fustani.cloud/swagger/index.html
[deploy] health-check OK
[deploy] deploy complete
```

Then independently verify:
```bash
ssh hm 'systemctl is-active hm && ls -la /var/www/hm/appsettings.json /var/www/hm/Secrets/firebase-service-account.json && du -sh /var/www/hm/uploads/'
curl -I https://hm.fustani.cloud/swagger/index.html
```
Expected: `active`, both preserved files exist, uploads dir has the expected size, HTTP 200.

If this fails: read the rollback log, fix the script in a new commit, repeat.

---

## Task 10: Tweak source-sync to always use main (post-smoke-test)

**Files:**
- Modify: `scripts/deploy.sh`

The Task 5 source-sync block already uses `git checkout main`. After Task 9's smoke test passes on the working branch, no change is required here — but **delete this task's note from the plan if you confirm Task 5's block is correct as written.**

- [ ] **Step 1: Verify Task 5's block hardcodes `main`**

Run:
```bash
grep -A2 "Sync source from origin/main" scripts/deploy.sh
```
Expected: shows `git checkout main` and `git reset --hard origin/main`.

If not, edit those two lines to hardcode `main`.

- [ ] **Step 2: Commit if changed (otherwise skip)**

If a change was needed:
```bash
git add scripts/deploy.sh
git commit -m "fix(deploy): hardcode main branch in source sync"
git push
```

---

## Task 11: Create the /deploy slash command

**Files:**
- Create: `.claude/commands/deploy.md`

- [ ] **Step 1: Create the slash command**

Create `.claude/commands/deploy.md`:
```markdown
---
description: Deploy current branch to production (merges to main, then runs scripts/deploy.sh on hm.fustani.cloud)
---

You are running the production deploy for HM. Execute this checklist exactly.

## Pre-flight (local)

1. Run `git status --porcelain`. If any output: STOP and tell the user "Working tree has uncommitted changes. Commit or stash before deploy."
2. Run `git rev-parse --abbrev-ref HEAD` to get the current branch.
3. If current branch is NOT `main`:
   - Tell the user: "Will fast-forward main to <branch> and push. OK?"
   - On 'yes': `git fetch origin && git checkout main && git merge --ff-only <branch> && git push origin main && git checkout <branch>`
   - If the fast-forward fails (non-FF): STOP and tell the user "main has diverged from <branch>; resolve manually before deploy."
4. If current branch IS `main`:
   - Run `git push origin main` to ensure remote is current.

## Confirm

Tell the user: "main is ready. Deploy now? Service will be briefly unavailable. [yes/no]"
Wait for explicit 'yes' before proceeding.

## Deploy

Run, streaming output:

    ssh hm "cd /opt/hm-source && git fetch origin && git reset --hard origin/main && bash scripts/deploy.sh"

If exit code is non-zero: surface the [deploy:FAIL] line and last 20 lines of output. Do NOT claim success.

## Post-deploy verification

Run:

    curl -fsS -o /dev/null -w "%{http_code}\n" https://hm.fustani.cloud/swagger/index.html

Expected: `200`. If anything else: tell the user "Health-check failed post-deploy; rollback may have triggered. Check `ssh hm 'journalctl -u hm -n 100'`."

## Report

Tell the user the short commit hash deployed and the verified HTTP 200 result. One sentence.
```

- [ ] **Step 2: Commit**

```bash
git add .claude/commands/deploy.md
git commit -m "feat(deploy): add /deploy slash command"
git push
```

- [ ] **Step 3: Test the slash command end-to-end**

In a fresh Claude Code session, type `/deploy` and walk through the flow on a no-op change (e.g. add a blank line to `README.md`, commit, then `/deploy`). Expected: pre-flight passes, user confirms, deploy runs and reports HTTP 200.

---

## Self-Review Notes

After writing the plan, I ran the spec-coverage check:

- Spec Section "One-time setup" → covered by Tasks 1, 2, 3.
- Spec Section "Per-deploy flow (server-side)" steps 1–11 → covered by Tasks 4–9 in the same order.
- Spec Section "Per-deploy flow (Claude side)" → covered by Task 11 (`/deploy` slash command).
- Spec "Files Preserved" → covered by Task 8 rsync excludes (verbatim).
- Spec "Failure Modes" → covered: build failure (Task 6 fails before service stop), migration failure (Task 7 explicit fail message, no swap), service start failure / health-check failure (Task 9 rollback function), concurrent deploy (Task 4 flock).
- Spec "nginx WebSocket check" → covered by Task 3 Step 7.

Type/identifier consistency: `STAGING_DIR`, `BACKUP_DIR`, `DEPLOY_DIR`, `SERVICE_NAME`, `HEALTH_URL`, `LOCK_FILE` are defined once in Task 4 and used consistently across Tasks 5–9.

No placeholders or TBDs. Each step shows exact commands and expected output.
