# Agent Setup Guide

Use this guide when you want an AI coding agent to help you set up this repo.

Paste this file into your agent, then give it access to the repo and your
terminal. Do not paste secrets into chat. Keep local files non-secret. Inject
credentials into each command from your secret manager.

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
- Confirmation that the Valheim server password is available in a secret
  manager. Do not ask the human to paste it into chat.
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
```

If admin SSH should use a private network, set:

```text
SSH_HOST=
```

`server.env` contains only non-secret operator settings. Do not add passwords,
cloud tokens, Tailscale keys, or R2 credentials. The scripts reject those
assignments before they source the file. Keep `server.env` ignored because it
can still contain private hostnames, IPs, and local paths.

The agent must use the operator's secret manager to inject only the required
process variables:

| Operation | Required process secret |
| --- | --- |
| Create or destroy a Hetzner VM without `HCLOUD_CONTEXT` | `HETZNER_TOKEN` or `HCLOUD_TOKEN` |
| Install the server or deploy server configuration | `VALHEIM_PASSWORD` |
| Server install with R2 enabled | `VALHEIM_R2_ACCESS_KEY_ID` and `VALHEIM_R2_SECRET_ACCESS_KEY` |

## Setup Steps

Run each command through the secret manager with only its required variables:

```bash
your-secret-manager run -- providers/hetzner/create.sh
your-secret-manager run -- scripts/install-server.sh
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

For R2 backups, request an R2 configuration deployment and add its non-secret
routing settings to `server.env`:

```text
VALHEIM_R2_CONFIGURE=1
VALHEIM_R2_ACCOUNT_ID=
VALHEIM_R2_BUCKET=
VALHEIM_R2_PREFIX=
```

`VALHEIM_R2_CONFIGURE=0` preserves an existing server runtime file. It does not
disable an R2 target that is already configured.

Inject the two R2 credential variables from the secret manager, then run:

```bash
your-secret-manager run -- scripts/install-server.sh
ssh root@<server> 'valheim-backup-and-upload'
```

Verify the object exists in R2.

## Safety Notes

- Do not store passwords, R2 credentials, cloud tokens, or Tailscale keys in
  local config, fixtures, logs, or chat.
- Do not commit world files, SSH private keys, private IPs, or local paths.
- Do not delete the VM until a recent backup has been downloaded or verified off-box.
- Do not enable automatic Valheim updates unless the human explicitly wants surprise restarts.
- Public SSH is optional; public Valheim UDP is required for normal player access.
- If a private network is added for SSH, keep UDP `2456-2458` public unless all players are also on that private network.
