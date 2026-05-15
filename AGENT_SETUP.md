# Agent Setup Guide

Use this guide when you want an AI coding agent to help you set up this repo.

Paste this file into your agent, then give it access to the repo and your
terminal. Do not paste secrets into chat unless you are comfortable with that.
Prefer local files, environment variables, or your secret manager.

## Goal

Set up a Valheim dedicated server using this repo.

The desired result is:

- A cloud VM running the official Valheim Dedicated Server.
- UDP `2456-2458` open for players.
- SSH/admin access working.
- An existing world uploaded, if provided.
- `valheim.service` active and enabled.
- Nightly local backups working.
- Optional R2 off-box backups working, if credentials are provided.

## Ask Me For

Before changing infrastructure, ask me for:

- Cloud provider target. This repo currently supports Hetzner.
- Hetzner location, or a player region if I do not know the exact location.
- Hetzner server type. Recommend `cpx21` for a small friend server.
- Hetzner SSH key name.
- Valheim server display name.
- Valheim world name.
- Valheim server password.
- Whether I have an existing world save to upload.
- Whether I want private admin SSH through Tailscale or another private network.
- Whether I want Cloudflare R2 backups.

Do not ask me to provide Valheim game files. The server should install the
official dedicated server through SteamCMD.

## Expected Local Config

Create `server.env` from the example:

```bash
cp examples/server.env.example server.env
```

Fill in at least:

```text
HETZNER_SERVER_NAME=
HETZNER_LOCATION=
HETZNER_SERVER_TYPE=
HETZNER_SSH_KEY=

VALHEIM_SERVER_NAME=
VALHEIM_WORLD_NAME=
VALHEIM_PASSWORD=
```

If admin SSH should use a private network, set:

```text
SSH_HOST=
```

Do not commit `server.env`.

## Setup Steps

Run:

```bash
providers/hetzner/create.sh
scripts/install-server.sh
```

If there is an existing world, upload both files:

```bash
scripts/upload-world.sh /path/to/WorldName.db /path/to/WorldName.fwl
```

The filenames must match `VALHEIM_WORLD_NAME`.

## Verification

Verify the service:

```bash
scripts/status.sh
```

Healthy output should show:

```text
valheim.service: active
Opened Steam server
Game server connected
```

Verify player connection by joining from Valheim:

```text
<server-public-ip>:2456
```

If joining works, the server copy of the world is now the source of truth.

## Backups

Verify local backups:

```bash
ssh root@<server> 'valheim-backup'
```

The backup archive should be created under:

```text
/var/backups/valheim/
```

For R2 backups, create `r2.env` from the example:

```bash
cp examples/r2.env.example r2.env
```

Fill in:

```text
VALHEIM_R2_ACCOUNT_ID=
VALHEIM_R2_BUCKET=
VALHEIM_R2_ACCESS_KEY_ID=
VALHEIM_R2_SECRET_ACCESS_KEY=
VALHEIM_R2_PREFIX=
```

Then run:

```bash
scripts/install-server.sh
ssh root@<server> 'valheim-backup-and-upload'
```

Verify the object exists in R2.

Do not commit `r2.env`.

## Safety Notes

- Do not commit world files, passwords, R2 credentials, cloud tokens, or SSH keys.
- Do not delete the VM until a recent backup has been downloaded or verified off-box.
- Do not enable automatic Valheim updates unless the human explicitly wants surprise restarts.
- Public SSH is optional; public Valheim UDP is required for normal player access.
- If a private network is added for SSH, keep UDP `2456-2458` public unless all players are also on that private network.

