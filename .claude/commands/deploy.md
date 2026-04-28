---
description: Deploy current branch to production (merges to main, then runs scripts/deploy.sh on hm.fustani.cloud)
---

You are running the production deploy for HM. Execute this checklist exactly.

## Pre-flight (local)

1. Run `git status --porcelain`. If any output: STOP and tell the user "Working tree has uncommitted changes. Commit or stash before deploy."
2. Run `git rev-parse --abbrev-ref HEAD` to get the current branch.
3. If the current branch is NOT `main`:
   - Tell the user: "Will fast-forward main to <branch> and push. OK?"
   - On 'yes':
     ```
     git fetch origin
     git checkout main
     git merge --ff-only <branch>
     git push origin main
     git checkout <branch>
     ```
   - If the fast-forward fails (non-FF): STOP and tell the user "main has diverged from <branch>; resolve manually before deploy."
4. If the current branch IS `main`:
   - Run `git push origin main` to ensure remote is current.

## Confirm

Tell the user: "main is ready. Deploy now? Service will be briefly unavailable. [yes/no]"
Wait for an explicit 'yes' before proceeding.

## Deploy

Run, streaming output:

    ssh hm "cd /opt/hm-source && git fetch origin && git reset --hard origin/main && bash scripts/deploy.sh"

If the exit code is non-zero: surface the `[deploy:FAIL]` line and the last 20 lines of output. Do NOT claim success.

## Post-deploy verification

Run:

    curl -fsS -o /dev/null -w "%{http_code}\n" https://hm.fustani.cloud/swagger/index.html

Expected: `200`. If anything else: tell the user "Health-check failed post-deploy; rollback may have triggered. Check `ssh hm 'journalctl -u hm -n 100'`."

## Report

Tell the user the short commit hash deployed and the verified HTTP 200 result. One sentence.
