# Portals

The Portals module makes naming and traveling through portals faster while
still waiting for the destination to finish loading.

## Behavior

- `Tab` cycles known portal tags that match the typed prefix.
- Seen and typed tags are remembered in a local BepInEx config file.
- Distant portal travel removes most of the fixed delay after the destination
  is ready.

## Status

- **Tested:** Faster portal transitions work in gameplay.
- **Needs test:** Tag autocomplete, fallback cycling, and remembered tags after
  relaunch.
