# Valheim 1.0 Migration

Valheim 1.0 may rewrite a world or character when it opens and saves it. That
is safe as long as 1.0 never opens the only copy. This migration therefore uses
one complete recovery set, one disposable rehearsal, and one stopped production
cutover. A failed migration restores the pre-1.0 copies and discards progress
made after cutover.

- **Status:** Planning
- **Release:** September 9, 2026
- **World decision:** Preserve the existing world.
- **Initial runtime:** Vanilla Valheim 1.0.
- **Repair tools:** Not part of the initial cutover.
- **Complete when:** Production is stable on 1.0 and each mod from the pre-1.0
  stack is restored, replaced, or explicitly deferred.

## Rules

- Do not let 1.0 open the only copy of a world or character.
- Do not test the production world first.
- Do not alternate one mutable world copy between pre-1.0 and 1.0 binaries.
- Start the rehearsal and production cutover in vanilla mode.
- Restore mods only after vanilla world, character, save, and reconnect checks
  pass.
- Keep Deep North repair and Upgrade World separate from the initial cutover.

Updating the game binaries alone does not change world or character data.
Opening and saving that data with 1.0 changes it. The pre-1.0 recovery set lets
you restore the pre-1.0 state, but rollback loses progress made after cutover.

## Build the Recovery Set

Quit the game clients and stop the server before taking the final copies.
Preserve:

1. The complete production world storage.
2. Every player's local and Steam Cloud character save roots.
3. The complete working dedicated-server installation, launcher, configuration,
   BepInEx files, and plugins.
4. A known-good client game and mod setup for each supported platform where
   practical.
5. The installed game builds, Benheim versions, plugin versions, and archive
   hashes needed to identify the working setup.

The world archive must exist locally and in R2. Download it, verify its hash,
and inspect it before migration day. Each player must verify that their backup
contains the expected `.fch` file. A backup is part of the recovery set only
after you restore it and inspect it.

The repo's supported backup and recovery paths own the commands. Use
`scripts/download-backups.sh` and the world archive inspection and restore
scripts rather than copying selected world files by hand.

## Rehearse on a Disposable Server

The rehearsal must use the final production world archive and the same Valheim
1.0 server build intended for production.

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

Start only when all players are disconnected and there is enough time to finish
or roll back.

1. Stop the production server.
2. Create one final backup while the production server is stopped. Upload it to
   R2 as the final pre-1.0 world archive.
3. Download and verify that final pre-1.0 world archive.
4. Archive the complete working pre-1.0 server installation.
5. Confirm every player's character backup is complete.
6. Confirm the production 1.0 build matches the passing rehearsal build.
7. Disable server mods through `scripts/set-server-mods.sh disable`.
8. Update the dedicated server and start the existing world in vanilla 1.0.
9. Join with the real characters and repeat the short world, character, save,
   restart, and reconnect smoke test.
10. Create and verify one post-migration world backup.

After vanilla production passes, enable only the mod layers that passed the
copied-world rehearsal. Use the supported server-mod installer for the
first-party stack. Do not assemble a new production stack by copying individual
plugin files.

## Roll Back

Rollback is a restore, not a downgrade in place.

1. Stop the production server.
2. Preserve the failed migrated world for later diagnosis.
3. Restore the pre-1.0 server installation into a clean directory.
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
