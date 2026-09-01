# Valheim monorepo development, testing, and operations

[`AGENTS.md`](AGENTS.md) owns agent behavior and the shared safety defaults.
This file owns monorepo-wide commands, tests, operating rules, and development
workflow. It also owns the server, migration, and public-repository workflows.
Nested `PROMPT.md` files add rules for their paths. Read the root
[`PRODUCT.md`](PRODUCT.md) before changing the product promise or a compatibility
boundary.

## Product boundaries

This repo has three related jobs:

- Provision and operate a Valheim dedicated server on a cloud VM.
- Support selected server-side mods with explicit player requirements.
- Build client mods under `client-mods/`, including quality-of-life and gameplay
  features. Keep server-assisted features explicit.

The root `PRODUCT.md` owns which clients and server components a shared feature
requires. A server-assisted feature must fail visibly when a required component
is missing or incompatible.

The root `PRODUCT.md` owns the overall server and mod promise. The client mod
at `client-mods/benheim/` owns its promise in `PRODUCT.md`, each user-facing
feature module owns its behavior and proof status in its own `PRODUCT.md`, and
each first-party server mod owns its behavior in a `PRODUCT.md` at the mod
root. Do not duplicate detailed feature behavior in the root product doc.

## Public repository rules

Follow the safety rules in `AGENTS.md`.

- Do not commit secrets, passwords, tokens, private IPs, world files, character
  files, Steam IDs, local save paths, or generated backup archives.
- Keep `server.env` non-secret. It may contain operator settings, private
  hostnames, IPs, and local paths, so it remains ignored.
- Secrets enter generic scripts only through the process environment. The
  scripts reject secret assignments in `server.env` before they source it.
- Treat restricted files under `/etc/valheim` as generated deployment
  artifacts, never secret sources of truth.
- Keep docs generic enough for other people to use the repo.
- Prefer small, explicit scripts over hidden local machine assumptions.
- Do not rely on ambient `hcloud` context. Inject `HETZNER_TOKEN` or
  `HCLOUD_TOKEN` into the provider process. Use a repo-owned `HCLOUD_CONTEXT`
  only when the operator intentionally configured it in `server.env`.

Keep provider lifecycle, server installation, world upload and download, and
backup logic separate.

## Offline character map inspection

Inspect a character map without opening Valheim or changing the save:

```bash
scripts/inspect-character-map.py /path/to/character.fch \
  --world-meta /path/to/world.fwl
```

The optional world metadata file confirms which world entry owns the character
map. Without it, the command labels entries by index. Use `--help` for JSON and
calibration options.

The character map does not store its pixel scale or the game's world radius.
The command prints the calibration it used. After a Valheim update, verify the
values installed with the game. Do this before you trust the distances or
override the defaults. The command rejects unsupported save and map formats
instead of guessing.

Keep character files and generated reports local. The summary omits IDs,
authors, coordinates, inventory, and raw map bytes, but player-created pin names
can still be private.

Run the synthetic proof with:

```bash
python3 tests/character-map-inspector-test.py
```

## Server development and operation

The server path provisions a cloud VM, installs SteamCMD and the official
Valheim Dedicated Server, manages systemd units, and backs up world files.
Provider lifecycle belongs under `providers/`. Server installation and
operations belong under `scripts/`, `systemd/`, and `backups/`.

Before a destructive server operation, follow the backup and compatibility
rules in `AGENTS.md`.

Useful operator commands are:

```bash
scripts/status.sh
scripts/logs.sh
scripts/restart.sh
scripts/download-backups.sh
scripts/apply-server-config.sh
scripts/install-server-mods.sh
scripts/set-server-mods.sh disable
```

`server/valheim-start` owns vanilla and modded launch paths. Set
`VALHEIM_MODDED=0` for recovery: it bypasses BepInEx without deleting installed
mod files or configuration. `PRODUCT.md` owns the exact approved first-party
plugin set. `scripts/install-server-mods.sh` owns its pinned package versions
and checksums and enforces that allowlist. It stages the whole stack before
downtime, takes one stopped-server backup, and restores the previous stack or
the vanilla path if installation fails. Do not auto-discover plugins from
`server-mods/`. New server mods must be removable without changing the world
save.

`scripts/apply-server-config.sh` owns routine deployment of the launcher and
the generated `/etc/valheim/server.env`. It combines non-secret local settings
with the process `VALHEIM_PASSWORD`, takes a stopped-server backup, and restores
the previous files if deployment fails. `scripts/install-server.sh` generates
`/etc/valheim/r2.env` only when configuration is requested and both process
credentials are present. A normal install leaves any existing R2 runtime file
unchanged. Native world settings belong in the launcher environment, not in a
replacement mod; `VALHEIM_PORTALS=casual` enables Valheim's own
metal-through-portals rule. `VALHEIM_SKILL_GAIN_RATE` controls skill gain.
`VALHEIM_SKILL_REDUCTION_RATE` controls skill loss on death for every player.

Keep the repository's mutating scripts secret-manager agnostic. Operators must
inject only the process variables required by the selected command. Do not add
an operator-specific credential wrapper, pull secrets into local files, or
print injected values. Scripts must finish credential preflight before their
first remote mutation. Use `tests/secret-flow-test.sh` when changing this
boundary.

Mark a server-mod gate complete in `PRODUCT.md` only after every named condition
passes. Record one-time rollout evidence in `MIGRATION-1.0.md`.

## Valheim 1.0 migration

Until the migration is closed and archived, `MIGRATION-1.0.md` is the canonical
one-time runbook for the September 9, 2026 upgrade. Update that runbook when
migration decisions, gates, commands, or proof change. Do not duplicate its
process in `PRODUCT.md` or treat it as permanent server doctrine.

Migration work must preserve a vanilla launch path, prove the world on a
temporary server before production, back up server worlds and client
characters, and restore mods only after vanilla 1.0 is stable.

## Benheim client development

`client-mods/benheim/PROMPT.md` owns Product Review, Benheim client development,
testing, installation, release, gameplay workflow, and client rules. Work under
`client-mods/benheim/` inherits those rules.

## Documentation map

- `README.md` is the public entrypoint.
- `PRODUCT.md` owns the overall server and mod product promise.
- `MIGRATION-1.0.md` temporarily owns the 1.0 cutover and mod-recovery process.
- `AGENT_SETUP.md` is for an AI agent helping a human set up a server.
- `AGENTS.md` owns agent behavior and points here for local workflow.
