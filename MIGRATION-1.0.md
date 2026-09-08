# Valheim 1.0 Migration

Valheim 1.0 supports existing worlds and characters. The first 1.0 save of the
current legacy world is still a one-way recovery boundary: the new save system
converts legacy world storage to a chunked directory layout, and 1.0 introduces
achievement and cheat-state metadata. This migration therefore uses one
complete recovery set, one disposable exact-build rehearsal, and one stopped
production cutover. A failed migration restores the pre-1.0 copies and discards
progress made after cutover.

- **Status:** Partial pre-release recovery proof complete; production access
  and the final stopped-server recovery set remain blocked.
- **Release:** September 9, 2026
- **World decision:** Preserve the existing world.
- **Initial runtime:** Vanilla Valheim 1.0.
- **Repair tools:** Not part of the initial cutover.
- **Availability:** Not a constraint. Production may remain stopped for days.
- **Priority:** No data loss first, demonstrated stability second, availability
  last.
- **Complete when:** Production is stable on 1.0 and each mod from the pre-1.0
  stack is restored, replaced, or explicitly deferred.

## Rules

- Do not let 1.0 open the only copy of a world or character.
- Do not test the production world first.
- Do not alternate one mutable world copy between pre-1.0 and 1.0 binaries.
- Start the rehearsal and production cutover in vanilla mode.
- Restore mods only after vanilla world, character, save, and reconnect checks
  pass.
- Keep production stopped while the exact release build is rehearsed. Do not
  shorten the freeze by weakening a recovery or stability gate.
- Do not use devcommands on a world or character that could later become the
  production copy. Valheim 1.0 can permanently mark world and character saves
  as cheated.
- Keep Deep North repair and Upgrade World separate from the initial cutover.

Updating the game binaries alone does not convert the world. Opening and saving
the world with 1.0 does. Iron Gate says existing saves remain usable, character
saves keep their current format, and the game creates a backup before converting
a non-chunked world. Those safeguards do not replace an independently restored
recovery set. Rollback loses progress made after cutover.

Iron Gate has confirmed that 1.0 releases September 9, supports existing saves,
and will not have a 1.0 public-test branch. Exact-build rehearsal therefore
cannot finish before release:

- [Valheim 1.0 FAQ](https://www.valheimgame.com/support/valheim-1-0-faq/)
- [Patch 0.221.13 public-test save-system notes](https://steamcommunity.com/app/892970/allnews/)

## Build the Recovery Set

Quit the game clients and stop the server before taking the final copies. Keep
the server stopped through rehearsal and production cutover. Preserve:

1. The complete production world storage.
2. Every player's local and Steam Cloud character save roots.
3. The complete working dedicated-server installation, launcher, BepInEx files,
   and plugins. Do not copy `/etc/valheim/server.env`; it contains the runtime
   password and is reproducible from the non-secret operator config plus
   Doppler.
4. An immutable copy and hash of the exact non-secret pre-1.0 operator config.
   Prove that it can render a fresh runtime environment with the scoped secret.
5. A known-good client game and mod setup for each supported platform where
   practical.
6. The installed game builds, Benheim versions, plugin versions, and archive
   hashes needed to identify the working setup.

The world archive must exist locally and in R2. Download it, verify its hash,
and inspect it before migration day. Each player must verify that their backup
contains the expected `.fch` file. A backup is part of the recovery set only
after you restore it and inspect it.

The repo's supported backup and recovery paths own the commands. Use
`scripts/download-backups.sh` and the world archive inspection and restore
scripts rather than copying selected world files by hand.

Before `valheim-update` runs, use
`scripts/archive-server-installation.sh` to archive the stopped pre-1.0
installation on the VM and in R2, then use `scripts/download-backups.sh` to
fetch it locally. The archive contains `/opt/valheim/server`, `/usr/local/bin/valheim-start`,
`/usr/local/bin/valheim-wait-ready`, and the systemd unit, but not
`/etc/valheim/server.env`. Extract it into an isolated directory, verify its
hash and Steam manifest, and start it on the disposable server before treating
it as a rollback asset. Render its runtime environment from the preserved
pre-1.0 operator config and the scoped password; boot-test that regenerated
configuration rather than reusing the excluded runtime file.

## Current Pre-release Evidence

The September 8 pre-release pass proved the parts that do not require Valheim
1.0 or production SSH:

- R2 contained `worlds-20260908T080015Z.tar.gz`. The downloaded archive is
  115,827,529 bytes with SHA-256
  `ede0957f977c5b26a039156a4206861cd11737ddfb2a910b9670f318ec8f50be`.
- The archive passed structural inspection and restored into an isolated local
  `worlds_local` directory. It contains the complete 12-file legacy world
  storage, including the current world pair and native backups.
- Ben's Steam Cloud and local character roots were copied into an ignored local
  recovery directory, copied back into an isolated restore directory without a
  diff, and the current real character parsed successfully. It is character
  format 43, contains one world entry, and matches world metadata format 37 for
  the preserved world.
- The exact pre-1.0 non-secret operator config was copied into the ignored
  recovery set with SHA-256
  `8b3f1e1392918fd8d1659b1e494189c5a1f76e3db77d3d320b246d5f3f4a637a`.
  It rendered a fresh password-bearing runtime file from the scoped secret; the
  output preserved the world, modded launch, portal, and skill settings and was
  then deleted.
- The archive and restore suite passed 34 checks. The secret-flow suite passed
  29 checks. The stopped-config helper passed 13 behavioral fixture checks,
  and the server-installation archive helper passed 14 behavioral fixture
  checks.
- The release-day operator config is prepared locally with server mods disabled
  and R2 configuration enabled. It has no secret assignments.

This is not the final recovery set. The server remained live while the R2 world
archive was created, the current dedicated-server build is not recorded, the
pre-1.0 installation is not archived, and other players' character copies are
not present. Production administration also remains unavailable: the local
Tailscale client is running, but the server is absent from the tailnet and SSH
times out. Do not update or open the production world with 1.0 until those four
gates are closed.

## Freeze Production and Finish the Recovery Set

Begin this phase only after production administration is restored and the
disposable rehearsal server is available. This is the single production freeze;
production stays stopped from the first backup through the passing rehearsal
and cutover.

1. Confirm every player is disconnected, then stop the production server.
2. Create one final world backup while the server is stopped and upload it to
   R2.
3. Download that exact archive, verify its hash, inspect it, and restore it into
   isolation. Record the archive name and hash in the execution record.
4. Run `scripts/archive-server-installation.sh` while production remains
   stopped. Download, verify, and extract that exact installation archive, then
   boot-test it on the disposable server with a newly rendered pre-1.0 runtime
   environment.
5. Confirm every player's character backup is complete.
6. Leave production stopped. Do not create a replacement “final” archive or
   resume pre-1.0 play after rehearsal begins; either action invalidates the
   rehearsal input and requires the rehearsal to restart.

## Rehearse on a Disposable Server

The rehearsal must use the final stopped-production world archive and the exact
Valheim 1.0 server build intended for production. Use a disposable Linux VM,
not a second mutable path on the production host. The prepared VM specification
is `valheim-1-0-rehearsal`, CPX21, Ubuntu 24.04, Ashburn, with a public IPv4 and
cleanup deadline of September 12. Provisioning remains blocked on explicit
spend approval. Do not run the current generic Hetzner create script unchanged:
its firewall opens SSH globally and its labels describe a durable game server.

1. Create a distinct disposable server.
2. Install the final Valheim 1.0 dedicated-server build with no mods enabled.
3. Restore a copy of the final production world archive.
4. Join first with a new test character.
5. Verify the world loads, saves, restarts, and accepts a reconnect.
6. Join with copies of the real characters.
7. Verify character inventory, equipment, progression, map state, and saves.
8. Check the main base, another distant base, portals, containers, tames, boss
   progression, building, combat, sleeping, and a server restart.
9. Record the exact Steam server build used for the passing rehearsal.

Do not promote the rehearsed world. It exists to prove conversion and runtime
behavior. Production later converts a fresh copy of the same stopped-server
archive on the same recorded build.

The new character separates server and world compatibility from character-save
compatibility. Never use the live character copies for rehearsal.

After vanilla passes, test the mod stack on the same copied world:

1. Server BepInEx and the current first-party server plugin stack.
2. Client BepInEx on Mac and Windows.
3. Benheim.
4. Any other mod deliberately selected for 1.0.

Add one layer at a time. At each layer, join the server, exercise that layer's
intended behavior, save, disconnect, restart the server, and rejoin. If a layer
fails, leave it disabled. Keep the last proven stack as the production
candidate.

If Steam publishes another server build after the rehearsal, rerun the relevant
rehearsal checks on that build. Production must not be the first world opened by
an unrehearsed build.

## Cut Over Production

Start only after the single production freeze, complete recovery proof, and
exact-build rehearsal all pass. Production must still be stopped, and the
recorded final world archive must still be the last pre-1.0 production state.
Time is intentionally not a gate.

1. Apply the prepared release-day config with
   `scripts/apply-server-config.sh --keep-stopped`. Confirm the generated
   runtime config says `VALHEIM_MODDED=0` and the service remains stopped.
2. With production still stopped, run `valheim-update`, record the installed
   dedicated-server build ID, and compare it with the passing rehearsal build.
3. Start the existing world in vanilla 1.0 only when the build IDs match.
4. Join with the real characters and repeat the short world, character, save,
   restart, and reconnect smoke test.
5. Create and verify one post-migration world backup.

After vanilla production passes, enable only the mod layers that passed the
copied-world rehearsal. Use the supported server-mod installer for the
first-party stack. Do not assemble a new production stack by copying individual
plugin files.

## Roll Back

Rollback is a restore, not a downgrade in place.

1. Stop the production server.
2. Preserve the failed migrated world for later diagnosis.
3. Move the failed 1.0 installation aside and restore the pre-1.0 server
   installation into a clean directory. Never extract it over the 1.0 tree.
4. Restore a fresh copy of that final pre-1.0 world archive.
5. Restore any character copy that 1.0 changed and that must return to its old
   state.
6. Start only after the server and clients can run the matching pre-1.0 build.

Do not merge old files over the 1.0 installation. Do not reopen the migrated
world with the old binary. Rollback intentionally discards post-cutover world
and character progress.

If Steam prevents an immediate client downgrade, keep the restored server
stopped until matching clients are available. The recovery set protects the
data even when game night cannot resume immediately.

## Deep North and World Repair

Do not explore more of the Deep North before 1.0. After vanilla migration,
inspect which 1.0 content is absent from previously explored areas and decide
whether the gaps matter.

Upgrade World is a possible repair tool, not part of migration. It can delete
world objects and reset generated zones, has no durable undo, and may mark the
world as cheated. Test it only on a fresh copied world after its source and
release explicitly support the installed 1.0 build. Promote a repaired archive
only after Ben separately accepts the exact world changes and achievement
tradeoff.

## Execution Record

Keep only evidence that changes a migration decision. Do not paste routine logs
or duplicate versions that package manifests already own.

| UTC time | Phase | Build or archive | Result | Decision |
| --- | --- | --- | --- | --- |
| 2026-09-08 18:58 | Tooling | world archive, secret-flow, stopped-config, and server-installation archive suites | 34/34, 29/29, 13/13, and 14/14 passed | Supported archive and secret boundaries are ready; the two new remote helpers are behaviorally fixture-backed until production access returns. |
| 2026-09-08 18:27 | Pre-release recovery | `worlds-20260908T080015Z.tar.gz`; SHA-256 `ede0957f977c5b26a039156a4206861cd11737ddfb2a910b9670f318ec8f50be` | Downloaded from R2, inspected, and restored locally. | Useful recovery proof, but not the final stopped-server archive. |
| 2026-09-08 18:28 | Character recovery | Ben's current Steam Cloud and local roots | Copied, restored without diff, and real character parsed against the preserved world metadata. | Ben's local recovery input is ready; other players remain a gate. |
| 2026-09-08 18:46 | Configuration recovery | Pre-1.0 operator config; SHA-256 `8b3f1e1392918fd8d1659b1e494189c5a1f76e3db77d3d320b246d5f3f4a637a` | Copied without secrets and used with the scoped password to render a fresh runtime environment. | Exact pre-1.0 settings are recoverable; boot proof remains part of the disposable rehearsal. |
| 2026-09-08 18:30 | Production access | Tailscale SSH | Timed out; server absent from local tailnet view. | Do not freeze, archive, update, or cut over until admin access is restored. |

## Close the Migration

Archive this runbook after:

- Production runs the existing world on Valheim 1.0.
- Real characters complete a normal session, save, restart, and reconnect.
- Local and R2 backups complete after migration.
- Each mod from the pre-1.0 stack is restored, replaced, or explicitly
  deferred.
- The vanilla recovery path still works.
- Disposable migration infrastructure is destroyed.

Before archiving, retain only the outcome, recovery identifiers, and unresolved
follow-up. Normal server operations continue to belong to `PROMPT.md` and the
operator scripts.
