# valheim-server Agent Context

This repo has three related jobs:

- Provision and operate a Valheim dedicated server on a cloud VM.
- Support selected server-side mods that remain compatible with vanilla clients.
- Build optional client-only quality-of-life mods under `mods/`.

Keep those boundaries clear. Server work should not assume client mods are
installed. Client mod work should not depend on server mods unless that product
direction changes explicitly. Read root `PRODUCT.md` for the overall server and
mod promise before changing compatibility boundaries.

## Public Repo Rules

- Do not commit secrets, passwords, tokens, private IPs, world files, character
  files, Steam IDs, local save paths, or generated backup archives.
- Treat `server.env`, `r2.env`, downloaded backups, and Valheim world/character
  files as local-only operator data.
- Keep docs generic enough for other people to use the repo.
- Prefer small, explicit scripts over hidden local machine assumptions.
- Do not rely on ambient `hcloud` context. Valheim provider scripts should get
  Hetzner auth from ignored `server.env` via `HETZNER_TOKEN`/`HCLOUD_TOKEN`, or
  an explicit repo-owned `HCLOUD_CONTEXT` only when the operator intentionally
  configured one.

## Server Work

The server path provisions a cloud VM, installs SteamCMD and the official
Valheim Dedicated Server, manages systemd units, and backs up world files.

- Provider lifecycle belongs under `providers/`.
- Server installation and operations belong under `scripts/`, `systemd/`, and
  `backups/`.
- Keep provider lifecycle, server installation, world upload/download, and
  backup logic separate.
- Before destructive server operations, download or verify backups first.
- Preserve vanilla-client compatibility for server-side mods unless the user
  explicitly changes that product promise.

Useful scripts:

```bash
scripts/status.sh
scripts/logs.sh
scripts/restart.sh
scripts/download-backups.sh
```

## Valheim 1.0 Migration

Until the migration is closed and archived, `MIGRATION-1.0.md` is the canonical
one-time runbook for the September 9, 2026 upgrade. Update it when migration
decisions, gates, commands, or proof change. Do not duplicate its process in
`PRODUCT.md` or treat it as permanent server doctrine.

Migration work must preserve a vanilla launch path, prove the world on a
temporary server before production, back up server world and client characters,
and restore mods only after vanilla 1.0 is stable.

## BenheimQoL Mod Work

BenheimQoL lives under:

```text
mods/benheim-qol/
```

`mods/benheim-qol/PRODUCT.md` owns the mod's product promise and detailed
user-visible behavior. Update it whenever a feature, shortcut, acceptance
expectation, or roadmap item changes.

Update root `PRODUCT.md` when a mod's role, runtime placement, client
requirement, or compatibility promise changes. Do not duplicate each mod's
detailed behavior there.

Manual test plans are task-scoped process artifacts, not canonical product
context. Derive the relevant checklist from the changed behavior, use it for the
current development pass, and do not accumulate it in `PRODUCT.md`.

Build and local install:

```bash
mods/benheim-qol/scripts/build.sh
mods/benheim-qol/scripts/install-local.sh
```

Expected build caveat:

- `System.Net.Http` version conflict warnings can appear from Valheim assembly
  references. They are known and acceptable when the build exits successfully.

Client mod rules:

- Keep BenheimQoL client-only unless the product direction changes explicitly.
- Do not add custom persistent world objects or custom item data casually.
- Keep the in-game shortcuts panel and `PRODUCT.md` aligned with implemented
  controls.
- Bump the visible plugin version when installing a user-testable behavior
  change so testers can verify the loaded DLL after relaunch.
- Valheim does not hot-reload the plugin DLL; after install, fully quit and
  relaunch the BepInEx-enabled game.

## Documentation

- `README.md` is the public entrypoint.
- `PRODUCT.md` owns the overall server and mod product promise.
- `MIGRATION-1.0.md` temporarily owns the 1.0 cutover and mod-recovery process.
- `AGENT_SETUP.md` is for an AI agent helping a human set up a server.
- `PROMPT.md` is the repo-wide development context for agents.
- `AGENTS.md` should point at `PROMPT.md` so both conventions stay in sync.
