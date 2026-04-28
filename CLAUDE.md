# CLAUDE.md

## Deploy workflow

After finishing a meaningful chunk of work (feature, fix, etc.) in this repo, **offer** to run `/deploy`. Never deploy unprompted — always wait for an explicit "yes" from the user.

The `/deploy` slash command (defined in `.claude/commands/deploy.md`) handles the full pipeline: FF-merge current branch to `main`, push, then run `scripts/deploy.sh` on the production server (`hm.fustani.cloud` via `ssh hm`).

The deploy script itself: backup → stop → migrate → swap → start → verify-health, with auto-rollback on failure. Preserves `appsettings.json`, `uploads/`, and `Secrets/`. See `docs/superpowers/specs/2026-04-28-auto-deploy-design.md` for the design and `scripts/deploy.sh` for the implementation.

Don't offer `/deploy` for trivial work (typo fixes in docs, formatting, comment edits) — only for changes that meaningfully affect runtime behavior.

## Git identity

This repo uses `Ma7mood202 <ma7mood.arks@gmail.com>` for commits (set as local config). Don't change this without explicit instruction.
