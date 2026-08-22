# Valheim 1.0 Migration Runbook

This is the temporary source of truth for moving the existing server, world,
characters, and mods to Valheim 1.0 on September 9, 2026. It owns the migration
until cutover and mod recovery are complete. It does not replace `PRODUCT.md`,
normal server operations, or each mod's product documentation.

- **Status:** Planning
- **Release:** September 9, 2026
- **Cutover target:** Existing world, initially running vanilla Valheim 1.0
- **Archive condition:** Vanilla 1.0 is stable, backups have run, and every
  chosen mod is restored or deliberately deferred.

## The Safe Shape

The migration has three phases:

1. Run a reversible, pinned mod setup before 1.0.
2. Prove the existing world on a temporary vanilla 1.0 server before updating
   the live server.
3. Restore mods one dependency layer at a time only after vanilla 1.0 is known
   good.

The live world is never the first copy opened by 1.0. The first 1.0 load uses a
backup on a temporary server. The live cutover starts only after that rehearsal
passes.

## Facts We Are Planning Around

- Iron Gate says existing worlds and saves remain usable in 1.0.
- New terrain, locations, and dungeons generate properly only in unexplored
  areas. Valheim treats roughly 500 metres around visited areas as explored.
- Iron Gate recommends a new world for the ideal experience but does not require one.
- There will be no public test branch for 1.0.
- Mods are not guaranteed to work at release and may prevent the game from
  starting until BepInEx and individual mods are updated.
- Achievements begin tracking after 1.0. Most cheat commands can mark a save as
  cheated, so migration validation must not use spawn or similar commands.
- The dedicated server does not auto-update. Clients may update through Steam
  before the operator updates the server, creating a temporary version mismatch.
- World state lives on the server. Character state lives with each player and
  must be backed up separately.

- [Valheim 1.0 FAQ](https://valheim.com/support/valheim-1-0-faq/)
- [Valheim FAQ: large world updates](https://valheim.com/faq/)
- [Official guidance for game updates with mods](https://valheim.com/support/getting-ready-for-the-ashlands/)
- [Dedicated server guide](https://valheim.com/support/a-guide-to-dedicated-servers/)

## Migration Boundaries

- Do not explore the Deep North before 1.0.
- Do not enable automatic server updates.
- Do not add mods that persist custom items, prefabs, or world objects before
  the migration.
- Do not load a world once with 1.0 and then casually reopen that migrated copy
  with the old server binary.
- Do not re-enable server and client mods during the initial vanilla cutover.
- Do not treat a successful process start as proof. A Mac client and a Windows
  client must both join, play, save, disconnect, and rejoin.

## Timeline

### Now Through August 31

Continue product design, source-code research, and upstream-mod evaluation for
future Benheim systems. Limit pre-1.0 implementation to important stability
work and migration preparation. Do not begin large gameplay-system
implementation until the vanilla Valheim 1.0 migration is complete and
Benheim's stable behavior has been ported and retested.

- [ ] Keep every server mod removable without changing the world save.
- [ ] Keep a tested vanilla launch path on the server.
- [ ] Keep a tested vanilla launch path on every Mac and Windows client.
- [ ] Pin mods and record the active set in the compatibility table below.
- [ ] Rehearse creation and destruction of the temporary migration server using
      the current game version.
- [ ] Rehearse restoring a downloaded world archive to that temporary server.
- [ ] Confirm no player has explored the Deep North or sailed along its edge.

### September 1 Through September 8

This is a change freeze, not a gameplay freeze. Keep playing, but do not change
the server runtime, world modifiers, BepInEx version, or mod set unless a change
is required to keep the server playable.

- [ ] Verify the most recent nightly backup exists locally and in R2.
- [ ] Run one cold backup while the server is stopped, then restart the server.
- [ ] Inspect the cold archive and verify it contains the complete active world
      storage.
- [ ] Verify each player can launch vanilla Valheim without removing mods.
- [ ] Have every player complete the character backup procedure below.
- [ ] Confirm the ignored temporary environment still targets a distinct VM.
- [ ] Announce the expected client/server update mismatch window.

### September 9

- [ ] Confirm the final 1.0 release is available for both clients and SteamCMD.
- [ ] Update vanilla clients, leaving modded launchers unused.
- [ ] Run and pass the temporary-server rehearsal with a production world copy.
- [ ] Stop production, take the final cold backup, and archive the old server
      installation.
- [ ] Update production, pass vanilla checks, restart, and take a fresh backup.

### After Vanilla Cutover

- [ ] Keep production vanilla through a normal session and nightly backup.
- [ ] Reintroduce server dependencies and mods in the order defined below.
- [ ] Port and prove stable Benheim behavior before adding new gameplay systems.
- [ ] Record restored, replaced, and deferred decisions below.
- [ ] Close and archive this runbook when the archive condition is satisfied.

## Mod Compatibility Table

Record migration decisions and evidence here. Package manifests and installed
files, not this table, own exact versions.

| Component | Runs on | September 9 disposition | 1.0 re-enable gate | Decision |
| --- | --- | --- | --- | --- |
| BepInEx | Server | Disabled | Server starts cleanly under the 1.0-compatible loader. | Deployed pre-1.0; 1.0 re-enable proof pending |
| Jotunn | Server | Remove | No re-enable gate if the first-party fire replacement passes. | Deployed pre-1.0 only for the failed third-party Eternal Fire stack; replacement removes this dependency |
| Third-party Eternal Fire | Server | Remove | None. Do not restore it. | Failed vanilla-client behavior test: after one manual fuel, a standing wood torch remained at `1/4`; an empty standing wood control torch did not relight. It was replaced by Benheim Eternal Fire. |
| Benheim Eternal Fire | Server | Disabled | Vanilla clients see existing zero-fuel pieces relight and burning pieces refill before they extinguish. This behavior survives a server restart and client reconnect. | Benheim Eternal Fire `0.1.1` is deployed on Valheim `0.221.12`. Existing empty fires and torches relit for a client that did not have Benheim Eternal Fire installed. Low-fuel and restart proof is pending. |
| Metal portals | Server, native | Reapply after vanilla proof | Restricted items pass through portals for vanilla clients after restart. | Passed portal traversal with a normally restricted metal item; restart proof pending. |
| BepInEx | Clients | Use vanilla launch | Mac and Windows clients launch and join with the compatible loader. | Pending |
| Benheim | Clients | Disabled | Benheim's stable behavior passes focused 1.0 testing. | Benheim `0.1.52` is the accepted stable pre-1.0 client on Valheim `0.221.12`. All regular players use version `0.1.52`. Final 1.0 proof is pending. |
| Future gameplay mods | To classify | Not admitted | Source audit identifies network ownership, persistence, and platform support. | Deferred |

## Character Backups

Every player owns this step because server backups exclude characters. Quit
Valheim and Steam before copying outside the live save directories.

### Mac Steam Client

Copy both save roots to a dated folder:

```text
~/Library/Application Support/IronGate/Valheim/
~/Library/Application Support/Steam/userdata/<STEAM_ID>/892970/
```

The first root covers local characters and other local Valheim state. The
second covers Steam Cloud state. Verify the backup contains at least one `.fch`
file before launching 1.0.

### Windows Steam Client

Copy both save roots to a dated folder:

```text
%USERPROFILE%\AppData\LocalLow\IronGate\Valheim\
<STEAM_INSTALL>\userdata\<STEAM_ID>\892970\
```

Resolve the real Steam path instead of assuming its default location.

### Cloud Conflict Rule

If Steam reports a cloud conflict, stop. Snapshot both roots again, compare
timestamps and inventory, then choose the known-good direction deliberately.

Record one row per player without committing names, Steam IDs, or save files:

| Client | Platform | Local root copied | Cloud root copied | `.fch` verified | Vanilla launch verified |
| --- | --- | --- | --- | --- | --- |
| Client A | Mac/Windows | [ ] | [ ] | [ ] | [ ] |
| Client B | Mac/Windows | [ ] | [ ] | [ ] | [ ] |
| Client C | Mac/Windows | [ ] | [ ] | [ ] | [ ] |

## Rehearse the Temporary Server Before Release

Before September, use the current version to prove the provider, installer,
world upload, firewall, and cleanup paths.

Create an ignored migration environment:

```bash
mkdir -p tmp/migration-1.0
cp examples/server.env.example tmp/migration-1.0/server.env
```

Copy the required non-secret settings from the ordinary `server.env` into the
migration file. Then edit the migration file before you run anything:

- Give `HETZNER_SERVER_NAME` a unique migration-only name.
- Give `VALHEIM_SERVER_NAME` a visibly temporary display name.
- Keep `VALHEIM_WORLD_NAME` unchanged so the uploaded pair still matches.
- Clear `SSH_HOST` so scripts cannot accidentally target production.
- Confirm the Hetzner location, size, SSH key, and operator secret profile.
- Keep passwords, cloud tokens, Tailscale keys, and R2 credentials out of the
  migration file.

Resolve the environment, inject each command's required process secrets through
the operator's secret manager, and inspect the target before creating anything:

```bash
export VALHEIM_ENV_FILE="$PWD/tmp/migration-1.0/server.env"
your-secret-manager run -- providers/hetzner/create.sh
your-secret-manager run -- scripts/install-server.sh
scripts/status.sh
```

Inspect a downloaded full-directory archive, then restore that exact storage
tree. The restore stops Valheim, verifies the uploaded archive checksum,
quarantines any existing `worlds_local` directory, and leaves the service
stopped after reporting the storage shape and installed Steam build:

```bash
scripts/inspect-world-archive.sh backups/<world-archive>.tar.gz
scripts/restore-world-archive.sh backups/<world-archive>.tar.gz
scripts/restart.sh
scripts/status.sh
```
Join the temporary server, verify the expected world, disconnect, and destroy
the migration VM. Read the resolved server name before confirming deletion:

```bash
your-secret-manager run -- providers/hetzner/destroy.sh
unset VALHEIM_ENV_FILE
```

Complete this lifecycle once before September.

## Release-Day Rehearsal

Repeat the temporary-server procedure after SteamCMD offers the final 1.0
release. Use the latest verified pre-cutover world backup. Install no BepInEx,
Jotunn, or plugins on the temporary server.

- [ ] Server logs show the expected world loading, Steam opening, and the game
      server connecting.
- [ ] A vanilla Mac client joins with the expected character.
- [ ] A vanilla Windows client joins with the expected character.
- [ ] The main base, representative distant base, portals, containers, tames,
      map state, boss progression, and world day look correct.
- [ ] Players can move items, build, fight, use a portal, and sleep normally.
- [ ] All clients disconnect cleanly.
- [ ] The server is restarted and both platforms can rejoin.
- [ ] State changed during the first session remains after restart.
- [ ] Logs contain no repeated save, network, or serialization errors.

Do not use developer spawn commands to make this faster. If a check fails,
leave production unchanged, preserve the temporary server and logs, and diagnose
before attempting the live cutover.

## Live Cutover

Run from the repo root with no players connected.

### Gate

- [ ] Release-day rehearsal passed.
- [ ] Every character backup row is complete.
- [ ] A recent world archive is present locally and in R2.
- [ ] The server has a proven vanilla launch path.
- [ ] The operator has enough uninterrupted time to finish or roll back.

### Stop and Preserve Production

`systemctl stop` uses the service's `SIGINT` path and waits for Valheim to exit.

```bash
bash <<'BASH'
set -euo pipefail
source scripts/lib.sh
load_config

remote_ssh 'systemctl stop valheim.service'
remote_ssh 'valheim-backup-and-upload'
remote_ssh '
  set -euo pipefail
  stamp=$(date -u +%Y%m%dT%H%M%SZ)
  archive=/var/backups/valheim/server-pre-1.0-$stamp.tar.gz
  tar -C /opt/valheim -czf "$archive" server
  sha256sum "$archive"
  valheim-r2-upload "$archive"
'
BASH

scripts/download-backups.sh
```

Verify both final archives downloaded, then inspect the world archive and list
the server archive. The world inspector reports legacy or directory-based
storage without assuming either one:

```bash
scripts/inspect-world-archive.sh backups/<final-world-archive>.tar.gz
tar -tzf backups/<server-pre-1.0-archive>.tar.gz | head
```

### Update and Start Vanilla 1.0

If server mods were installed before 1.0, first switch the repo-managed launcher
back to its previously tested vanilla mode. The migration is blocked until that
switch is implemented and proven; do not remove random plugin files on release
day and hope the loader stays dormant.

```bash
bash <<'BASH'
set -euo pipefail
source scripts/lib.sh
load_config

remote_ssh 'valheim-update'
remote_ssh 'systemctl start valheim.service'
BASH

scripts/status.sh
```

Healthy status shows the correct world loading, Steam opening, and server
connection. On failure, collect logs before changing anything:

```bash
scripts/logs.sh
```

### Accept Production

Repeat the release-day rehearsal checks against production. Then prove that 1.0
can persist new state:

1. Make one harmless, recognizable world change.
2. Have every player disconnect.
3. Restart with `scripts/restart.sh`.
4. Rejoin from Mac and Windows and confirm the change remains.
5. Stop and run `valheim-backup-and-upload` once more.
6. Start the server and download the new post-migration archive.

Record the release version, archive names, status output, and result in the
execution ledger. Do not put passwords, IPs, Steam IDs, or save contents there.

## Rollback

Rollback if the world fails to load, expected structures or progression are
missing, saves repeatedly error, clients cannot remain connected, or state does
not survive a restart. Do not rollback for a single broken mod because no mods
should be active yet.

1. Inspect the final pre-1.0 world archive again.
2. Run `scripts/restore-world-archive.sh` with that archive. It stops the
   service and quarantines the migrated storage without merging it.
3. Replace `/opt/valheim/server` from the `server-pre-1.0` archive.
4. Confirm ownership remains `valheim:valheim`.
5. Start the old vanilla server only if clients can also run the matching old
   game version. Otherwise leave the service stopped and wait for a game hotfix
   or a deliberate client downgrade.

A server rollback does not automatically downgrade Steam clients. Data safety
comes first; resuming game night is a separate decision.

Never alternate the same mutable world copy between old and 1.0 binaries. Each
attempt starts from a fresh copy of the preserved pre-1.0 archive.

## Restore Mods After Vanilla Is Stable

Use a copy of the post-migration world on the temporary server for server-mod
validation. Use backed-up characters and a non-production session for risky
client-mod validation.

### Upstream references for mod recovery

[Jere Kuusela's Valheim repositories](https://github.com/JereKuusela) are the
first external source to inspect when Valheim 1.0 changes a native interface
that Benheim uses. The repositories show current examples of the mechanisms
Benheim uses for locations, prefabs, server commands, and world updates. They
show how an implementation can work. Benheim's product contracts still define
what each feature must do.

- [valheim-dev](https://github.com/JereKuusela/valheim-dev) shows current
  server-executed commands, native location lookup, permission checks, and
  temporary minimap pins. Its `find` command is the reference for a Benheim
  marker that shows only the location selected by the server.
- [valheim-upgrade_world](https://github.com/JereKuusela/valheim-upgrade_world)
  shows how Valheim stores, regenerates, filters, and repairs location
  instances across world versions. Before using its world-edit operations,
  prove them safe in a rehearsal on a copy of the production world. Until
  then, use them only as references.
- [valheim-expand_world_data](https://github.com/JereKuusela/valheim-expand_world_data)
  shows current location registration, location generation, and data-driven
  world configuration.
- [valheim-expand_world_prefabs](https://github.com/JereKuusela/valheim-expand_world_prefabs)
  shows current prefab discovery and loading through Valheim's asset system.
- [valheim-world_edit_commands](https://github.com/JereKuusela/valheim-world_edit_commands)
  and [valheim-infinity_hammer](https://github.com/JereKuusela/valheim-infinity_hammer)
  show current administrator selection, visualization, and world-edit command
  boundaries.

At migration time, inspect the current upstream source and license before
copying anything. Pin the exact upstream commit only when Benheim adopts code
or a behavioral pattern. Do not import a broad upstream command or world-edit
framework to recover one narrow Benheim feature.

Restore in this order:

1. Server BepInEx.
2. Benheim Eternal Fire.
3. Client BepInEx on Mac and Windows.
4. Benheim.
5. Any newly selected gameplay mods.

For every layer:

- Confirm the release explicitly supports the installed 1.0 build.
- Read upstream issues and release notes for save or multiplayer problems.
- Start with only that layer and already-proven dependencies enabled.
- Inspect startup logs for exceptions and dependency warnings.
- Join from every supported client platform.
- Exercise the feature, disconnect, restart, and rejoin.
- Confirm vanilla launch remains available on each client.
- Update the compatibility table before proceeding.

If a mod fails, disable only that layer and its unused dependencies. Keep
production on the last proven stack. Replacing or porting a mod is normal; it is
not a reason to hold the world migration open indefinitely.

## Close and Archive

The migration is complete when:

- [ ] Production runs Valheim 1.0 on the existing world.
- [ ] Mac and Windows clients have completed normal play sessions.
- [ ] A restart preserved post-1.0 state.
- [ ] A nightly local and R2 backup completed after migration.
- [ ] Every pre-migration mod is restored, replaced, or explicitly deferred.
- [ ] Vanilla server and client launch paths still work.
- [ ] Temporary migration infrastructure is destroyed.
- [ ] Root `PRODUCT.md`, `PROMPT.md`, and operator docs reflect the lasting
      runtime rather than the migration process.

Then change the status to complete, remove live pointers, and move this file to
`docs/archive/valheim-1.0-migration.md`. Before archiving, collapse the execution
ledger to the useful outcome, retained backup identifiers, and any unresolved
follow-up. Do not preserve a wall of routine command output.

## Execution Ledger

Keep this empty until a rehearsal or cutover action actually occurs.

| UTC time | Phase | Evidence or artifact | Result | Follow-up |
| --- | --- | --- | --- | --- |
