# Benheim client workflow and rules

Product behavior, controls, player feedback, acceptance meaning, and proof
status belong in the owning `PRODUCT.md`. Keep implementation details in code
or a deeper technical document.

Keep loose ideas in `IDEAS.md`. Do not put an unimplemented candidate in
`PRODUCT_REVIEW.md`; add it only after the candidate is packaged and installed
for live acceptance.

## Product review

`../../PRODUCT_REVIEW.md` supports Ben's product review: expected behavior,
open product questions, and remaining playtests. The integration lead adds
unproven product checks and updates the installed version when installation
changes what Ben can test. Include technical references when they help run a
product check or interpret its result. Keep development and release records
out of Product Review; query their sources when needed.

Ben and the Project Lead own acceptance judgments. An integration lead must
not:

- mark behavior from its own release as accepted;
- remove passed items based only on static proof; or
- promote behavior into accepted `PRODUCT.md` truth.

Each review item states the shortest player action and decision-changing
outcome. Keep telemetry schemas, implementation invariants, and exhaustive
edges in code, tests, or deeper technical docs. Reserve programmatic or log
proof for hidden, ambiguous, or destructive boundaries such as conservation,
ownership, networking, persistence, and credentials. After Ben or the Project
Lead accepts an item, the integration lead removes it from the queue. The
Project Lead updates the owning product document.

## Build and release

Use the canonical verification entrypoint for every client change:

```bash
client-mods/benheim/scripts/verify.sh
```

`verify.sh` runs every `client-mods/benheim/tests/*-test.sh` source and installer
check, the Quick Stack summary checks, and the Release DLL build. It does not
install files, touch a Valheim game directory, create a platform package, or
publish a release. The Release DLL build and Quick Stack summary checks may
write ignored `bin/` and `obj/` outputs. Use `verify.sh` instead of running its
checks separately.

A `System.Net.Http` version conflict warning from Valheim assembly references
is acceptable if the build exits successfully.

Inspect installed Valheim source with:

```bash
client-mods/benheim/scripts/decompile-valheim.sh Character
client-mods/benheim/scripts/ensure-valheim-source.sh
client-mods/benheim/scripts/search-valheim-source.sh -n 'StackAll\('
client-mods/benheim/scripts/list-valheim-types.sh projectile
client-mods/benheim/scripts/diff-valheim-types.sh --help
```

The decompile helper caches source by the exact assembly SHA-256 and requested
type. It writes decompiled source to standard output. It writes the resolved
assembly path, SHA-256, requested type, and whether the cache was hit or missed
to standard error.

Build, install locally on Mac, and package both platforms with:

```bash
client-mods/benheim/scripts/build.sh
client-mods/benheim/scripts/install-local.sh
client-mods/benheim/scripts/package-macos.sh
client-mods/benheim/scripts/package-windows.sh
```

`install-local.sh` must run the same Mac installer shipped to players. The
installer must be safe to run repeatedly. Keep BepInEx installation,
legacy-plugin cleanup, and launcher generation in it. The Mac launcher starts
Steam when needed before Valheim.

Normal Steam launch remains vanilla on Mac and Windows. `Benheim.app` on Mac
and the managed `Benheim` shortcut on Windows are the explicit modded paths.
Launchers and installers must not check a network source for updates. Share
complete platform packages; players update by rerunning the installer while
Valheim is closed.

The Mac launcher starts the installed BepInEx script only after Steam's
connection log shows a successful login. Do not use `ipcserver` as the
readiness signal because it can remain after Steam exits.

The Windows installer keeps UnityDoorstop disabled in `doorstop_config.ini`.
Its managed shortcut starts Steam, finds Valheim across configured libraries,
and launches `valheim.exe` with `--doorstop-enabled true`. Do not rename
Doorstop DLLs to switch modes. Remove retired updater apps, shortcuts, and state
only when a managed identifier or marker proves ownership. Leave unrelated
paths unchanged.

The Windows installer must:

- find Valheim in configured Steam libraries;
- verify the pinned BepInEx archive;
- disable the standalone MassFarming plugin;
- refuse to overwrite an unrelated desktop shortcut; and
- keep normal Steam launch vanilla.

Use `client-mods/benheim/tests/windows-installer-test.sh` to verify installer
source and packaged files until a Windows CI runner can execute the installer.

Before this task installs or launches a packaged build for bounded startup
proof, confirm that no Valheim process is running. Any Valheim process that was
already running is a hard stop. Do not quit or kill it, install over it, or
launch or relaunch around it. Wait for Ben's explicit instruction.

Install the exact packaged artifact that will be shared. Launch it through
Benheim's managed path and reach Valheim's real main menu. Verify that the log
shows the expected version, session start, and chainloader completion. Confirm
that the log contains no Harmony, core-disablement, or gameplay-disabled
markers. A task may quit only the Valheim process that it launched for this
bounded startup proof. Quit it cleanly after validation. Do not enter a world. One
clean startup is the normal gate. Launch again only when an active incident
requires more evidence.

`release.sh` publishes Benheim only from a clean local `main` that exactly
matches `origin/main`. It runs `verify.sh`, packages both platforms from that
build, creates the `benheim-v<version>` GitHub release, and uploads stable
`Benheim-macOS.zip` and `Benheim-Windows.zip` assets. Release assets are
distribution artifacts, not an update channel.

## Gameplay development loop

For a gameplay change:

1. Complete the public-mod source gate before you propose a design or edit
   gameplay code. The gate is required for features, fixes, feasibility spikes,
   UI, and supporting mechanics, even when Ben does not repeat it in the task
   prompt.
   - Inspect working implementations in the original source of public Valheim
     mods. Search for the exact Valheim or Unity mechanism that the change
     needs, not only mods with the same product idea. Package pages and
     descriptions are not source.
   - In the first technical handoff, name each useful candidate. Include its
     original-source URL, exact version or commit, and license. State the narrow
     implementation concept and whether to reuse it, adapt it, use it only as
     evidence, or reject it, with the reason.
   - If no candidate fits, state what the search covered and which required
     mechanism was absent. Do this before you design a local solution.
   - Reuse or adapt a proven implementation concept without inheriting the
     other mod's product assumptions. Treat source with no license or an
     incompatible license as evidence only.
2. Before coding against a native runtime asset, inspect each required asset
   after Valheim loads it in a world. Use `bhcatalog effects|text|ui [filter]`
   when that command covers the asset. Otherwise, add
   the smallest focused probe for identity, components, hierarchy, and
   readiness. Source names, decompiled code, and mocks are not runtime proof.
3. Preserve the observed contract in focused tests. Rerun the probe against the
   candidate build.
4. Use [Developer Diagnostics](src/DeveloperDiagnostics/PRODUCT.md) for every
   feature's runtime evidence. Record relevant attempts, blocking decisions,
   state changes, and actual outcomes through the shared typed pipeline.
   Verify the behavior itself. Confirm that the evidence explains what happened,
   including why actions were rejected. Reuse sufficient existing events;
   reserve verbose inspection for registered probes.
5. Bump the visible version and install while Valheim is fully quit.
6. Relaunch, reproduce, and record what the player tried.
7. Query `[diag]` events. Read the server journal only for server-owned behavior.
8. Fix observed failures and repeat until gameplay and evidence agree.

Use `Diagnostics.Emit(DiagnosticEvent)` for feature evidence. It writes readable
`[diag]` output and local `BepInEx/BenheimEvents.ndjson`, then routes the typed
event to configured remote diagnostics. `Diagnostics.Event(...)` writes only
text and does not satisfy this contract. Keep normal BepInEx warnings and errors.
Use
`client-mods/benheim/scripts/query-events.py --help` to stream current or
archived events, filter fields, or find starts without a terminal event.

Normal packages stay credential-free. Use scoped secrets with
`client-mods/benheim/scripts/package-private-test.sh`. Rotate its token if an
archive leaves Ben, Johnny, and Ozi or before public release.

## Client rules

- Keep one Benheim client DLL.
- Before changing Put Away, follow the nested [Inventory development
  guide](src/Inventory/PROMPT.md). Its shared protocol owns authority,
  conservation, correlation, and convergence. Do not replace that protocol
  with requester-local `Container.StackAll()` or another cached chest write.
- Apply protected-item filtering whenever `Inventory.StackAll()` moves items
  out of the local player's inventory. This covers Valheim's **Place stacks**
  and **Hold to stack** actions. Put Away filters the same protected items
  before owner-routed reservation. All three actions keep manually pocketed,
  equipped, and hotbar items protected. Manual item moves and **Take all** stay
  unchanged.
- Defer custom persistent world objects until a specific feature needs them.
  Its design must cover world effects, recovery, migration, and removal. Add
  custom item data only when the product needs it and removal cannot corrupt a
  character.
- Build the Valheim-styled menu with Unity UI and loaded native templates.
  Before every client version bump or package build, compare the Benheim menu
  catalog with the owning `PRODUCT.md` files. Update and organize each new or
  changed player-facing control or feature.
- Bump the visible plugin version when installing a user-testable change so
  testers can verify the loaded DLL after relaunch.
- Valheim does not hot-reload the plugin DLL. Fully quit and relaunch the
  BepInEx-enabled game after installation.
