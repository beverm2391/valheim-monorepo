# valheim-server Agent Context

This repo has two related jobs:

- Provision and operate a vanilla Valheim dedicated server on a cloud VM.
- Build optional client-only quality-of-life mods under `mods/`.

Keep those boundaries clear. Server work should not assume client mods are
installed. Client mod work should not require the dedicated server to install
mods unless a user explicitly changes that product direction.

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
- Preserve the vanilla server assumption unless the user explicitly requests
  server-side mods.

Useful scripts:

```bash
scripts/status.sh
scripts/logs.sh
scripts/restart.sh
scripts/download-backups.sh
```

## BenheimQoL Mod Work

BenheimQoL lives under:

```text
mods/benheim-qol/
```

`mods/benheim-qol/PRODUCT.md` is the canonical product reference for the mod.
Update it whenever a feature, shortcut, version, acceptance expectation,
troubleshooting note, or roadmap item changes.

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
- `AGENT_SETUP.md` is for an AI agent helping a human set up a server.
- `PROMPT.md` is the repo-wide development context for agents.
- `AGENTS.md` should point at `PROMPT.md` so both conventions stay in sync.
