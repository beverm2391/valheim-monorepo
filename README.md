# valheim-server

Run a Valheim dedicated server on a small cloud VM.

This repo is for the common friend-group case: you already have a Valheim world,
you want it online all the time, and you do not want to keep your gaming PC
running under your desk. It provisions a normal Linux VM, installs the official
Valheim Dedicated Server with SteamCMD, runs it as a systemd service, and backs
up the world files.

Hetzner Cloud is the first provider because Valheim dedicated servers want
simple infrastructure: a stable public IP, UDP ports, persistent disk, and a
boring Linux service. The provider scripts are separate from the installer so
other clouds can be added later.

The server and optional mod product direction is tracked in
[`PRODUCT.md`](PRODUCT.md).

## What You Get

- A Hetzner Cloud VM and firewall.
- The official Valheim Dedicated Server installed through SteamCMD.
- A `valheim.service` systemd service that starts on boot and restarts on crash.
- Scripts to upload an existing legacy world save or restore a complete backup.
- Local nightly backups on the VM.
- Optional Cloudflare R2 uploads for off-box backups.
- Optional pinned server mods with a vanilla-client compatibility requirement.
- No bundled game files, world saves, passwords, or cloud credentials.

## How It Works

There are three layers:

- `providers/hetzner/` creates or deletes the cloud machine and firewall.
- `scripts/install-server.sh` installs SteamCMD, Valheim, systemd units, and backup tools.
- `scripts/upload-world.sh` copies an initial legacy `.db` / `.fwl` world pair.
- `scripts/restore-world-archive.sh` replaces the complete world storage tree from a backup.

The active world lives on the server at:

```text
/var/lib/valheim/worlds_local/
```

Nightly backups archive that folder to:

```text
/var/backups/valheim/
```

If R2 is configured, the same tarball is uploaded off-box.

## Requirements

- `hcloud`, authenticated with a Hetzner Cloud project.
- An SSH key already added to Hetzner Cloud.
- `ssh`, `scp`, and `rsync`.
- A Valheim world pair for initial legacy import, or a full `worlds_local`
  backup archive for restore.

For a small friend server, a `cpx21` in the nearest region is a good starting
point. You can try a smaller box later, but 4 GB RAM keeps the first setup
boring.

## Quick Start

Copy the example config:

```bash
cp examples/server.env.example server.env
```

Edit `server.env`:

```bash
HETZNER_SERVER_NAME=valheim
HETZNER_LOCATION=ash
HETZNER_SERVER_TYPE=cpx21
HETZNER_SSH_KEY=your-hetzner-ssh-key-name

VALHEIM_SERVER_NAME=My Server
VALHEIM_WORLD_NAME=MyWorld
VALHEIM_PASSWORD=change-me-min-5-chars
```

Create the VM and firewall:

```bash
providers/hetzner/create.sh
```

Install the server:

```bash
scripts/install-server.sh
```

Upload your world:

```bash
scripts/upload-world.sh /path/to/MyWorld.db /path/to/MyWorld.fwl
```

Check status:

```bash
scripts/status.sh
```

Follow logs:

```bash
scripts/logs.sh
```

Apply later launcher or server-setting changes without rerunning provisioning:

```bash
scripts/apply-server-config.sh
```

This stops Valheim, takes a backup, installs the repo launcher and local
`server.env`, then restarts the service. A failed deployment restores the
previous launcher and environment.

## Using An AI Agent

If you want an AI coding agent to walk you through setup, give it this repo and
paste in [`AGENT_SETUP.md`](AGENT_SETUP.md). That file tells the agent what to
ask you, which scripts to run, how to verify the server, and which files must
not be committed.

## Connecting From Valheim

Use Valheim's normal Join Game flow.

If the server appears in the community list, join it there. If not, direct
connect to the server's public IP and port:

```text
<server-ip>:2456
```

The default Valheim port is `2456`. The Hetzner firewall opens UDP
`2456-2458`.

## Moving an Existing World

Valheim worlds are stored as matching `.db` and `.fwl` files. The world name in
`server.env` must match the filenames without the extension.

For example:

```text
VALHEIM_WORLD_NAME=MyWorld
MyWorld.db
MyWorld.fwl
```

On macOS with Steam Cloud, worlds are commonly under:

```text
~/Library/Application Support/Steam/userdata/<steam-id>/892970/remote/worlds/
```

After you upload a world and play on the server, the server copy becomes the
source of truth. Your old local save will not stay in sync automatically.

## Private Admin Access

The Valheim server should stay publicly reachable for players, but SSH does not
have to be public.

If you add private networking such as Tailscale, set `SSH_HOST` in `server.env`
to the private IP or hostname:

```text
SSH_HOST=100.x.y.z
```

The scripts will use that host for SSH while players continue to use the public
Valheim IP.

## Backups

Valheim creates its own backups inside its world storage. This repo also
installs a systemd timer that archives the full `worlds_local` folder nightly.
The repo archive preserves the entire directory rather than selecting known
extensions, so the same backup path covers legacy `.db` / `.fwl` saves and
directory-based chunked saves.

For a legacy world, that means each backup includes files such as:

```text
MyWorld.db
MyWorld.fwl
MyWorld_backup_auto-*.db
MyWorld_backup_auto-*.fwl
```

Local backups live on the VM:

```text
/var/backups/valheim/worlds-YYYYMMDDTHHMMSSZ.tar.gz
```

Download them to your machine:

```bash
scripts/download-backups.sh
```

Inspect a downloaded archive before using it:

```bash
scripts/inspect-world-archive.sh backups/worlds-YYYYMMDDTHHMMSSZ.tar.gz
```

The inspector validates the archive paths, reports its checksum and storage
shape, and hashes recognizable metadata files. It treats directory names as
evidence rather than a schema because Valheim's chunked layout may still
change.

To restore the complete archive to the configured server:

```bash
scripts/restore-world-archive.sh backups/worlds-YYYYMMDDTHHMMSSZ.tar.gz
```

Restore stops `valheim.service`, verifies the uploaded archive checksum,
extracts into a staging directory, and moves the old `worlds_local` directory
to a timestamped `.quarantine-*` path before installing the replacement. It
does not merge files and leaves Valheim stopped so you can review the printed
storage and Steam build metadata. Start it only after that output looks right:

```bash
scripts/restart.sh
scripts/status.sh
```

Local VM backups protect against bad saves. Off-box backups protect against
losing the VM.

## Cloudflare R2 Backups

R2 backups are optional but recommended for worlds you care about.

Create an R2 bucket and S3-compatible credentials, then configure `r2.env`
locally:

```bash
cp examples/r2.env.example r2.env
```

Required values:

```text
VALHEIM_R2_ACCOUNT_ID=
VALHEIM_R2_BUCKET=valheim-backups
VALHEIM_R2_ACCESS_KEY_ID=
VALHEIM_R2_SECRET_ACCESS_KEY=
VALHEIM_R2_PREFIX=my-server
```

Run the installer again:

```bash
scripts/install-server.sh
```

The installer copies `r2.env` to `/etc/valheim/r2.env` with restricted
permissions. The nightly backup timer uploads tarballs to:

```text
s3://<bucket>/<prefix>/worlds-YYYYMMDDTHHMMSSZ.tar.gz
```

Run a manual backup and upload:

```bash
ssh root@<server> 'valheim-backup-and-upload'
```

The uploader uses `rclone` and skips bucket checks so scoped R2 object tokens do
not need bucket-management permission.

## Updating Valheim

For the September 9, 2026 Valheim 1.0 upgrade, follow the temporary
[`MIGRATION-1.0.md`](MIGRATION-1.0.md) runbook instead of the ordinary update
sequence below.

When your client updates and the server needs to match, SSH to the server and
run:

```bash
sudo systemctl stop valheim
sudo valheim-update
sudo systemctl start valheim
```

Automatic updates are intentionally not enabled. Surprise restarts during a
session are worse than a manual update before game night.

## Server Mods

The server can run a small, pinned mod stack. Server mods must remain
compatible with vanilla clients. The repo-managed stack is:

- BepInEx, the plugin loader.
- Benheim Eternal Fire, a first-party server plugin that makes ordinary fires,
  torches, hearths, and braziers never require manual refueling.

Benheim Eternal Fire updates Valheim's native world-object fuel field. Vanilla
clients receive the normal synchronized state and do not need the plugin. It
refills supported pieces at one native fuel unit, before Valheim considers them
empty, rather than keeping their displayed fuel pinned at maximum. Existing
empty pieces are initialized when their world objects load. In Valheim's current
`Fireplace` implementation, burning requires fuel above zero and one unit lasts
one complete `m_secPerFuel` interval; that interval is the synchronization
margin. The allowlist excludes cooking stations, smelters, blast furnaces, and
eitr refineries.

The pinned plugin binary is built from the source in this repo. Maintainers can
rebuild it against their current Valheim installation with:

```bash
server-mods/benheim-eternal-fire/scripts/build.sh
```

Install or refresh the pinned stack:

```bash
scripts/install-server-mods.sh
```

The installer verifies BepInEx and plugin checksums, stages every file before
downtime, stops Valheim, takes a stopped-server backup, uploads it when R2 is
configured, installs the files, and starts the modded launch path. Success
requires both ordinary server readiness and the exact Benheim Eternal Fire load
message from that start. A failed install switches back to vanilla and verifies
that the recovered server reaches readiness. The installer also removes the
retired Jotunn and third-party Eternal Fire files.

Bypass BepInEx and restart immediately on the vanilla launch path:

```bash
scripts/set-server-mods.sh disable
```

Re-enable the installed stack:

```bash
scripts/set-server-mods.sh enable
```

Disabling the stack leaves its files and configuration in place and restarts
Valheim without BepInEx.

## Metal Portals

Valheim has a native dedicated-server rule for carrying normally restricted
items through portals, so this behavior does not require a mod. Set:

```text
VALHEIM_PORTALS=casual
```

Then run:

```bash
scripts/apply-server-config.sh
```

The launcher validates this setting and passes Valheim's official
`-modifier portals casual` argument. Leave `VALHEIM_PORTALS` empty to avoid
setting a portal modifier from the command line.

## Skill Progression

Valheim can scale skill gain and death loss for the whole world without a mod.
Both settings are percentages of the normal rate, where `100` keeps vanilla
behavior. For example:

```text
VALHEIM_SKILL_GAIN_RATE=150
VALHEIM_SKILL_REDUCTION_RATE=20
```

This increases skill gain to 1.5 times the normal rate. Valheim normally removes
5% of every skill on death. Setting the reduction rate to `20` applies 20% of
that penalty, resulting in a 1% loss. The normal 10-minute protection after a
death still applies.

Run `scripts/apply-server-config.sh` after changing either value. Leave a value
empty to avoid setting that native world key from the command line.

## Destroying the Server

Download backups first:

```bash
scripts/download-backups.sh
```

Then delete the Hetzner server:

```bash
providers/hetzner/destroy.sh
```

If you use R2 backups, confirm the latest archive is present off-box before
destroying the VM.

## What This Does Not Do

- It does not copy or redistribute Valheim binaries.
- It does not manage arbitrary modpacks or client installations.
- It does not provide a web dashboard.
- It does not automatically update Valheim.
- It does not make the server ephemeral or scale-to-zero.

The goal is a small, durable, understandable dedicated server.
