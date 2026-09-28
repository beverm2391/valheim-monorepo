# Native Build Pieces

Benheim makes selected existing Valheim pieces more useful while the separate
Benheim Building mod takes shape.

## Current Behavior

- With the crystal-support change introduced in `0.1.107`, crystal walls can be
  stacked and can carry other pieces placed on top of them in local play.

## In Development

- Pieces extending beyond structural support should still show Valheim's native
  placement feedback. Stacked crystal walls should remain stable after a reload.
- Ben reported that existing crystal walls broke when Ozi joined on `0.1.106`.
  That version lacks the crystal-support change; whether zone ownership caused
  the breakage is unconfirmed. Mixed-version multiplayer stability needs proof
  before this behavior is treated as safe for the group.
