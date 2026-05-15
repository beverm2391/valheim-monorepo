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

## What You Get

- A Hetzner Cloud VM and firewall.
- The official Valheim Dedicated Server installed through SteamCMD.
- A `valheim.service` systemd service that starts on boot and restarts on crash.
- Scripts to upload an existing world save.
- Local nightly backups on the VM.
- Optional Cloudflare R2 uploads for off-box backups.
- No bundled game files, world saves, passwords, or cloud credentials.

## How It Works

There are three layers:

- `providers/hetzner/` creates or deletes the cloud machine and firewall.
- `scripts/install-server.sh` installs SteamCMD, Valheim, systemd units, and backup tools.
- `scripts/upload-world.sh` copies your `.db` / `.fwl` world files onto the server.

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
- A Valheim world pair: `WorldName.db` and `WorldName.fwl`.

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

Valheim creates its own backup files beside the active world files. This repo
also installs a systemd timer that archives the full `worlds_local` folder
nightly.

That means each backup includes:

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

When your client updates and the server needs to match, SSH to the server and
run:

```bash
sudo systemctl stop valheim
sudo valheim-update
sudo systemctl start valheim
```

Automatic updates are intentionally not enabled. Surprise restarts during a
session are worse than a manual update before game night.

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
- It does not manage mods.
- It does not provide a web dashboard.
- It does not automatically update Valheim.
- It does not make the server ephemeral or scale-to-zero.

The goal is a small, durable, understandable dedicated server.
