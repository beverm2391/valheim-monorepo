# valheim-server

Run a small Valheim dedicated server on a plain cloud VM.

The first provider is Hetzner Cloud because Valheim wants boring VM networking:
a stable public IP, UDP ports, persistent disk, and a normal Linux service. The
scripts keep the machine lifecycle separate from the game installer so other
providers can be added later.

## What this does

- Creates a Hetzner Cloud VM and firewall.
- Installs the Valheim Dedicated Server through SteamCMD.
- Runs Valheim with systemd.
- Uploads an existing world save.
- Keeps Valheim's native world backups and adds nightly tarball backups.

It does not include Valheim binaries, world files, passwords, or cloud tokens.

## Requirements

- `hcloud` authenticated with a Hetzner Cloud project.
- An SSH key already added to Hetzner Cloud.
- `ssh`, `scp`, and `rsync`.
- A Valheim world pair: `WorldName.db` and `WorldName.fwl`.

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

Install Valheim:

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

## Connecting

In Valheim, use the normal Join Game flow. You can join through the server list
when public listing works, or direct-connect to the Hetzner server IP on the
configured port.

The default Valheim port is `2456`. The Hetzner firewall opens UDP
`2456-2458`.

## Existing Worlds

Valheim worlds are stored as matching `.db` and `.fwl` files. The world name in
`server.env` must match the filenames without the extension.

For example:

```text
VALHEIM_WORLD_NAME=first
first.db
first.fwl
```

On macOS with Steam Cloud, worlds are commonly under:

```text
~/Library/Application Support/Steam/userdata/<steam-id>/892970/remote/worlds/
```

## Backups

Valheim creates its own world backups beside the active world files. This repo
also installs a systemd timer that archives the server's `worlds_local` folder
nightly to:

```text
/var/backups/valheim/
```

Download those archives with:

```bash
scripts/download-backups.sh
```

## Updating Valheim

SSH to the server and run:

```bash
sudo systemctl stop valheim
sudo valheim-update
sudo systemctl start valheim
```

## Destroying the Server

Download backups first:

```bash
scripts/download-backups.sh
```

Then delete the Hetzner server:

```bash
providers/hetzner/destroy.sh
```

## Provider Model

The intended boundary is:

- Provider scripts create, destroy, and expose a reachable machine.
- Install scripts configure SteamCMD, Valheim, systemd, and backups.
- World scripts upload, download, and restore save files.

That keeps durable game hosting separate from any particular cloud backend.
