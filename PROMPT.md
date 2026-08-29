# valheim-server development, testing, and operations

[`AGENTS.md`](AGENTS.md) owns agent behavior and the shared safety defaults.
This file owns the commands, development workflow, tests, and operating rules
for this repository. Read the root [`PRODUCT.md`](PRODUCT.md) before changing
the product promise or a compatibility boundary.

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

## Benheim development and testing

The Benheim client mod lives under `client-mods/benheim/`. Product behavior,
controls, player feedback, acceptance meaning, and proof status belong in the
owning product document. Keep implementation details in code or a deeper
technical document.

Ben and the Project Lead own `PRODUCT_REVIEW.md` as the live acceptance queue;
Dev Leads may provide evidence or investigate ambiguity, but do not own it.
Each item states the shortest player action and decision-changing outcome.
Keep telemetry schemas, implementation invariants, and exhaustive edges in
code, tests, or deeper technical docs; behavior and acceptance meaning stay in
the owning `PRODUCT.md`. Player-observable feel can accept clear, low-risk
tuning or presentation. Reserve programmatic or log proof for hidden,
ambiguous, or destructive boundaries such as conservation, ownership,
networking, persistence, and credentials. After acceptance, remove the item
and update the owning product document.

Build and test a client-only change with the canonical verification entrypoint:
```bash
client-mods/benheim/scripts/verify.sh
```

`verify.sh` runs every `client-mods/benheim/tests/*-test.sh` source/installer
check, the quick-stack summary checks, and the Release DLL build. It does not
install files, touch a Valheim game directory, create a platform package, or
publish a release. The build and quick-stack checks may write ignored `bin/`
and `obj/` outputs. Use `verify.sh` instead of running its checks separately.

The expected build caveat is a `System.Net.Http` version conflict warning from
Valheim assembly references. It is acceptable when the build exits
successfully.

Inspect one type from the installed Valheim assembly:

```bash
client-mods/benheim/scripts/decompile-valheim.sh Character
```

The helper caches decompiled source by the exact assembly SHA-256 and requested
type. It writes decompiled source to standard output. It writes the resolved
assembly path, SHA-256, requested type, and cache hit or miss to standard error.

Cache and search the complete installed assembly:

```bash
client-mods/benheim/scripts/ensure-valheim-source.sh
client-mods/benheim/scripts/search-valheim-source.sh -n 'StackAll\('
client-mods/benheim/scripts/list-valheim-types.sh projectile
client-mods/benheim/scripts/diff-valheim-types.sh --help
```

Build and install locally on Mac, then package for Mac and Windows:

```bash
client-mods/benheim/scripts/build.sh
client-mods/benheim/scripts/install-local.sh
client-mods/benheim/scripts/package-macos.sh
client-mods/benheim/scripts/package-windows.sh
```

`install-local.sh` must run the same Mac installer shipped to players. The installer must
be safe to run repeatedly. Keep BepInEx installation, legacy-plugin cleanup, and launcher
generation in that installer. The Mac launcher must start Steam when needed before it starts Valheim.

The normal Steam launch remains vanilla on Mac and Windows. `Benheim.app` on Mac and the
managed `Benheim` shortcut on Windows are the explicit modded launch paths. Launchers and
installers must not check GitHub or another network source for updates. Share updates as
complete platform packages; a player updates by rerunning the installer while Valheim is closed.

The Mac launcher starts the installed BepInEx launch script only after Steam's connection
log shows a successful login. Do not use `ipcserver` as the readiness signal because it can remain after Steam exits.

The Windows installer keeps UnityDoorstop disabled in `doorstop_config.ini`. Its managed shortcut starts Steam,
finds Valheim across configured Steam libraries, and launches `valheim.exe` with `--doorstop-enabled true`.
Do not rename Doorstop DLLs to switch modes. Remove retired updater apps, shortcuts, and state only when a
managed identifier or marker proves ownership. Leave unrelated paths unchanged.

The Windows installer must:

- find Valheim in configured Steam libraries;
- verify the pinned BepInEx archive;
- disable the standalone MassFarming plugin;
- refuse to overwrite an unrelated desktop shortcut; and
- keep the normal Steam launch vanilla after installation.

Use `client-mods/benheim/tests/windows-installer-test.sh` to verify installer source and
packaged files. Keep this test until a Windows CI runner can execute the installer.

Before distributing a Benheim client candidate, install the exact packaged artifact that will be shared.
Launch it through the managed Benheim path to Valheim's real main menu. Verify that the log shows the expected
version, session start, and chainloader completion, with no Harmony, core-disablement, or gameplay-disabled markers.
Then quit cleanly without entering a world. One clean packaged-build startup is the normal gate.
Repeat the launch only when an active incident requires more evidence.

When `release.sh` publishes Benheim, it must run only from a clean local `main`
that exactly matches `origin/main`. It runs `verify.sh`, packages both
platforms from that verified build, creates the `benheim-v<version>` GitHub
release, and uploads stable `Benheim-macOS.zip` and `Benheim-Windows.zip`
assets. Release assets are distribution artifacts, not an update channel.

## Gameplay development loop

For a gameplay change:

1. Add diagnostics only when acceptance depends on a result the player cannot
   reliably see or a hidden, ambiguous, or destructive invariant. Reuse evidence
   that already answers the product question.
2. Bump the visible version and install while Valheim is fully quit.
3. Relaunch, reproduce, and record what the player tried.
4. Query `[diag]` events; read the server journal only for server-owned behavior.
5. Fix observed failures and repeat until gameplay and evidence agree.

Diagnostic events use `[diag][Feature] action key=value`. Log actions, important
decisions, and results, not every frame. Keep normal BepInEx warnings and errors.
Benheim also writes each event to `BepInEx/BenheimEvents.ndjson`. Use
`client-mods/benheim/scripts/query-events.py --help` to stream current or archived
events, filter fields, or find starts without a terminal event.

After a world loads, `bh debug catalog effects|text|ui [filter]` previews native
runtime sources and atomically replaces `BepInEx/BenheimRuntimeCatalog.ndjson`.
Readiness failures are visible; the bounded snapshot stays local.

Normal packages stay credential-free. Use scoped secrets for
`package-private-test.sh`; rotate its token if an archive leaves Ben, Johnny,
and Ozi or before public release.

## Client mod rules

- Keep one Benheim client DLL.
- Before changing Put Away, follow the nested [Inventory development
  guide](client-mods/benheim/src/Inventory/PROMPT.md). Its shared protocol owns the
  authority, conservation, correlation, and convergence rules. Do not replace
  that protocol with requester-local `Container.StackAll()` or another cached
  chest write.
- Apply protected-item filtering whenever `Inventory.StackAll()` moves items
  out of the local player's inventory. The filter applies to Valheim's **Place
  stacks** and **Hold to stack** actions. Put Away filters the same protected
  items before its owner-routed reservation. All three actions must keep
  manually pocketed, equipped, and hotbar items protected. Manual item moves
  and **Take all** remain unchanged.
- Defer custom persistent world objects until a specific feature needs them.
  That feature design must cover their effects on the world, recovery,
  migration, and removal. Add custom item data only when the product design
  requires it and removal cannot corrupt a character.
- Build the Valheim-styled Benheim menu with Unity UI and loaded native
  templates. Before every client version bump or package build, compare its
  catalog with owning `PRODUCT.md` files; update and organize every new or
  changed player-facing control or feature.
- Bump the visible plugin version when installing a user-testable behavior
  change so testers can verify the loaded DLL after relaunch.
- Valheim does not hot-reload the plugin DLL. After installation, fully quit and relaunch the BepInEx-enabled game.

## Documentation map

- `README.md` is the public entrypoint.
- `PRODUCT.md` owns the overall server and mod product promise.
- `MIGRATION-1.0.md` temporarily owns the 1.0 cutover and mod-recovery process.
- `AGENT_SETUP.md` is for an AI agent helping a human set up a server.
- `AGENTS.md` owns agent behavior and points here for local workflow.
