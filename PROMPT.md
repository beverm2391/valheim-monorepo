# valheim-server Agent Context

This repo has three related jobs:

- Provision and operate a Valheim dedicated server on a cloud VM.
- Support selected server-side mods that remain compatible with vanilla clients.
- Build optional quality-of-life mods under `mods/`. Keep server-assisted
  features explicit.

Keep those boundaries clear. Most server work must not assume client mods are
installed. A server-assisted client feature must disable itself when a required
client component is missing. Read root `PRODUCT.md` before changing
compatibility boundaries.

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
scripts/apply-server-config.sh
scripts/install-server-mods.sh
scripts/set-server-mods.sh disable
```

`server/valheim-start` owns the vanilla and modded launch paths.
`VALHEIM_MODDED=0` is the recovery path: it bypasses BepInEx without deleting
installed mod files or configuration. `scripts/install-server-mods.sh` owns the
pinned package versions and checksums, stages downloads before downtime, takes
a stopped-server backup, and falls back to the vanilla path if installation
fails. Keep new server mods removable without changing the world save. Benheim
Inventory is the one server plugin that coordinates a client feature. It must
not prevent a vanilla client from joining.

`scripts/apply-server-config.sh` owns routine deployment of the launcher and
`server.env`. It takes a stopped-server backup and restores the previous files
if deployment fails. Native world settings belong in the launcher environment,
not in a replacement mod; `VALHEIM_PORTALS=casual` enables Valheim's own
metal-through-portals rule. `VALHEIM_SKILL_GAIN_RATE` controls skill gain.
`VALHEIM_SKILL_REDUCTION_RATE` controls skill loss on death for every player.

Mark a server-mod gate complete in `PRODUCT.md` only after every named condition
passes. Record one-time rollout evidence in `MIGRATION-1.0.md`.

## Valheim 1.0 Migration

Until the migration is closed and archived, `MIGRATION-1.0.md` is the canonical
one-time runbook for the September 9, 2026 upgrade. Update it when migration
decisions, gates, commands, or proof change. Do not duplicate its process in
`PRODUCT.md` or treat it as permanent server doctrine.

Migration work must preserve a vanilla launch path, prove the world on a
temporary server before production, back up server world and client characters,
and restore mods only after vanilla 1.0 is stable.

## Benheim Client Mod Work

The Benheim client mod lives under:

```text
mods/benheim-qol/
```

`mods/benheim-qol/PRODUCT.md` owns the overall mod promise and feature index.
The `PRODUCT.md` in each user-facing feature folder owns that feature's behavior
and current test status. Product behavior includes lightweight player-facing UI,
feedback, and interaction expectations; keep implementation details in code or
deeper technical docs. Update the owning feature document when behavior,
controls, player experience, test results, or development status changes.

Each first-party server mod with player-facing behavior owns one `PRODUCT.md` at
the mod root. That document owns the mod's behavior, player experience, and
proof status.

When the player reports gameplay results, update the owning feature document
before completing the task. Move confirmed behavior to **Current Behavior**.
Keep failed or unproven behavior in **In Development**. Delete behavior the
player no longer wants.

Update the repository `PRODUCT.md` when a mod moves between the client and
server or changes its compatibility requirements. Do not duplicate each mod's
detailed behavior there.

Manual test plans are task-scoped process artifacts, not canonical product
context. Derive the relevant checklist from the changed behavior, use it for the
current development pass, and do not accumulate it in `PRODUCT.md`.

Build and install locally on Mac, then package for Mac and Windows:

```bash
mods/benheim-qol/scripts/build.sh
mods/benheim-qol/scripts/install-local.sh
mods/benheim-qol/scripts/package-macos.sh
mods/benheim-qol/scripts/package-windows.sh
```

`install-local.sh` must run the same Mac installer shipped to players. The
installer must be safe to run repeatedly. Keep BepInEx installation,
legacy-plugin cleanup, and launcher generation in that installer. The launcher
must start Steam when needed and wait until Steam's interprocess communication
(IPC) service is ready before it starts Valheim.

The Windows installer must:

- find Valheim in configured Steam libraries;
- verify the pinned BepInEx archive;
- disable the standalone MassFarming plugin; and
- refuse to overwrite an unrelated desktop shortcut.

Use `mods/benheim-qol/tests/windows-installer-test.sh` to verify the installer
source and packaged files. Keep this test until a Windows CI runner can execute
the installer.

Publish Benheim with `mods/benheim-qol/scripts/release.sh`. The command must run
only from a clean local `main` that exactly matches `origin/main`. The script:

- runs the complete client test suite;
- builds both platform packages;
- creates the `benheim-v<version>` GitHub release; and
- uploads the stable `Benheim-macOS.zip` and `Benheim-Windows.zip` assets.

The first install uses the stable package for Mac or Windows. The installer adds
a separate updater named `Update Benheim`. Before launch, each launcher briefly
checks the stable `VERSION` file. If the check fails or times out, the launcher
continues with the installed version. When a newer stable version exists, the
launcher offers `Update and launch` or `Launch current version`. Only the updater
can change files. It verifies the package against `SHA256SUMS.txt` and reruns the
installer while Valheim is closed.

Use this development loop for gameplay changes:

1. Add concise diagnostic events for the changed action and the decisions that
   control it.
2. Bump the visible plugin version and install the new DLL while Valheim is
   fully quit.
3. Ask the player to relaunch, reproduce the behavior, and report what they
   tried.
4. Read `<Valheim>/BepInEx/LogOutput.log` and filter for `[diag]` events.
5. Read the server journal only when code for the behavior runs on the server or
   the behavior depends on a server response.
6. Fix the observed failure, reinstall, and repeat until gameplay and logs agree.

Diagnostic events use `[diag][Feature] action key=value`. Log player actions,
important decisions, and results. Do not log every frame. Keep normal BepInEx
warning and error logging enabled.

Expected build caveat:

- `System.Net.Http` version conflict warnings can appear from Valheim assembly
  references. They are known and acceptable when the build exits successfully.

Client mod rules:

- Keep one Benheim client DLL. Shared inventory protocol source lives under
  `shared/benheim-inventory-protocol/` and compiles into both BenheimQoL and the
  Benheim Inventory server plugin.
- Read `shared/benheim-inventory-protocol/PROTOCOL.md` before changing Put Away.
  That file owns requirements for protocol versions, chest ownership,
  transactions, retries, journals, receipts, reservations, item restoration,
  and recovery. Follow those requirements instead of restating them here.
- Add custom persistent world objects or custom item data only when the product
  or protocol design explicitly requires them.
- Keep the in-game shortcuts panel and the owning feature `PRODUCT.md` aligned
  with implemented controls.
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
