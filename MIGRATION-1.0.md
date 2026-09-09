# Valheim 1.0 temporary migration checklist

[The curated migration doc](docs/server/1.0-migration.html) owns Ben's
decisions. [The evidence file](knowledge-base/server/1.0-evidence.md) owns the
research. This file exists only to execute the migration safely, then gets
deleted.

The pre-1.0 Mac and Linux binaries are verified locally and in R2. Their
decompiled source is verified locally. The snapshot manifests own their exact
build identities and hashes.

## Preserve the production world

1. Confirm that no players are connected, then stop the server.
2. Trigger one fresh world backup and R2 upload.
3. Confirm the normal backup job succeeded.
4. Leave production stopped and unchanged until the QA-proven build is ready.

If anything fails, stop and investigate. Do not use devcommands or spawned
items on the canonical world during migration.

## Prove vanilla in disposable QA

1. Create a temporary Hetzner QA server: CPX21, Ashburn, Ubuntu 24.04 x86,
   local disk only, provider backups off.
2. Install the exact Valheim 1.0 dedicated-server build with mods disabled.
3. Restore a disposable copy of the preserved production world.
4. Start the copied world on vanilla 1.0.
5. Join with real characters, play, and save.
6. Restart the server and rejoin.

Record three identities for the passing QA run:

- the dedicated server's Steam build ID for app `896660`;
- the joining client's Steam build ID for app `892970`;
- Valheim's client/server compatibility version.

The two Steam build IDs identify different apps and are not expected to match.
The QA-to-production gate compares the dedicated-server build ID only.

Do not promote the QA world. It is disposable proof; production will open its
own preserved world only after the software build passes QA. If 1.0 converts
the world's save format, that conversion happens first to the disposable QA
copy. The converted QA copy proves the migration path but never becomes the
canonical world.

## Preserve the 1.0 porting inputs

1. Preserve the 1.0 Mac client and Linux server assemblies, matching BepInEx
   and mod binaries, Steam build IDs, and hashes using the same shape as the
   pre-1.0 snapshot.
2. Verify the raw snapshot locally and in R2.
3. Decompile the preserved client and server `assembly_valheim.dll` files
   locally with `client-mods/benheim/scripts/ensure-valheim-source.sh`.
4. Compare the decompiled pre-1.0 and 1.0 source by mod area.

Raw snapshots belong locally and in R2. Decompiled source is derived and stays
local.

After vanilla QA passes, production conversion and QA mod restoration are
independent lanes. Run them in parallel when useful.

## Convert production to vanilla 1.0

1. Confirm Steam's current dedicated-server build for app `896660` is still
   the build that passed QA. If it changed, repeat vanilla QA on the new server
   build before touching the production world.
2. Update production with all mods disabled.
3. Read production's app `896660` manifest and confirm its build ID matches the
   QA-passed dedicated-server build ID. If it differs, do not start the world.
4. Start the untouched frozen pre-1.0 production world and let the production
   1.0 server perform its own save-format conversion if required. Do not copy
   the converted QA world into production.
5. Join, play, save, restart the server, and rejoin.
6. Trigger and confirm a fresh vanilla-1.0 world backup and R2 upload.

Until step 6 succeeds, keep the frozen pre-1.0 world backup and matching old
server installation as one recovery set. Never open a 1.0-converted production
world with the old server binaries.

## Port mods on QA

1. Prove BepInEx and the standalone Valheim Dev bridge without gameplay mods.
2. Run the entire existing mod stack on QA before rewriting anything.
3. Leave working mods alone. Turn observed failures into independent mod-area
   assignments that agents can handle in parallel.
4. Use the existing diagnostics rather than creating a migration logger:
   actionable client, Harmony, BepInEx, and dedicated-server failures go to the
   same searchable Axiom surface as bounded structured records. Local NDJSON,
   normal BepInEx logs, and the systemd journal remain the raw fallbacks.
5. One integration owner builds, installs, and tests each combined candidate on
   QA. Parallel agents may edit separate mod areas, but they do not run
   competing builds into the same output directories.
6. Repeat until the exact candidate binaries and configuration pass the join,
   play, save, restart, and rejoin checks on QA.

Fix forward from observed failures. A failed mod stays disabled while unrelated
working mods continue.

## Promote the proven mods

1. Install only the exact mod binaries and configuration that passed QA onto
   the already-proven production 1.0 server.
2. Join, play, save, restart the server, and rejoin.
3. Trigger and confirm a fresh post-mod-restoration world backup and R2 upload.

Delete this checklist after production is stable on 1.0, every mod is working or
explicitly deferred, the post-migration backup succeeds, and the disposable QA
server is destroyed.
