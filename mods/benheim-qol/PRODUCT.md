# Benheim

Benheim is our curated Valheim gameplay mod. It combines quality-of-life
features, balance changes, gameplay adjustments, and selected new mechanics to
make the game more fun for our group. It is neither a total overhaul nor only a
quality-of-life mod.

Benheim should feel continuous with Valheim. It should first deepen or improve
Valheim through familiar actions, progression, resources, structures,
creatures, and its look and feel instead of creating a parallel system. Most
current features are client-only. Our regular group runs compatible Benheim
versions. Versions need not be identical if they preserve the behavior and
shared data of every Benheim feature the group uses. Benheim does not promise
multiplayer compatibility for players who do not run it.

This file owns the overall product promise. Each feature module has a
`PRODUCT.md` that owns its behavior and proof status.

## Product Philosophy

Benheim exists to improve our actual play, not to satisfy a mod category. A
small convenience can belong beside a substantial new mechanic when both make
the game better. Existing quality-of-life features remain part of Benheim and
support larger gameplay systems, but they do not define the whole product.

Choose work where the gameplay we want, continuity with Valheim, and a clean,
feasible implementation meet. Reuse mature upstream infrastructure when it
fits. Own the product decisions, configuration, balance, and connections
between systems instead of inheriting another mod's assumptions.

Prefer small, legible rules that combine Valheim's native states, costs,
controls, and outcomes into emergent gameplay before inventing a bespoke
subsystem. Deeper mechanics remain welcome when their gameplay value earns
their complexity.

Complexity and variation are welcome when players can learn them through the
world and use that knowledge to make meaningful choices. Prefer connected
systems that create new plans, builds, discoveries, and stories. Avoid feature
soup, configuration complexity that players must manage, and breadth that does
not improve play.

Benheim can create deliberately brutal challenges and give players satisfying
ways to overcome them. Severe threats should reward skill, preparation, builds,
equipment, tools, knowledge, and satisfying counters. Difficulty without
meaningful player agency is incomplete. Do not remove a threat merely because
it is oppressive; connect it to satisfying ways for players to overcome it.

Preserve the current world, characters, and recognizable Valheim progression
unless a deliberate product decision justifies a compatibility cost. Benheim
is not trying to replace Valheim. It is a practical, evolving version of the
game that is more fun for our group.

## Combat Direction

Benheim deepens skill-based combat, game depth, and variation by extending
Valheim's existing combat behavior, mechanics, and abstractions. Three
connected domains own this work: Enemy Tiers, Affinities, and Weapon Rhythm.

Enemy Tiers is the conceptual starting point, but development is not a strict
sequence. Make small playable changes across all three domains
instead of completing one full system before starting another. Begin with a
specific mechanic and generalize it only after gameplay proves a reusable
pattern. Judge a mechanic by gameplay value first. Then choose the
implementation that fits Valheim and returns the most gameplay value for its
technical cost.

## Product Rules

- Keep one client DLL and one simple install.
- Ship one idempotent installer for each desktop platform. Each installer must
  leave unrelated files and launchers unchanged. It must refuse installation
  while Valheim is running.
- Share updates as complete packages. A player updates by rerunning the
  installer. Benheim does not check GitHub or another network source for
  updates.
- The Mac launcher starts Steam when needed before it starts modded Valheim.
- The Windows installer finds Valheim across Steam libraries and creates a
  desktop shortcut. It keeps UnityDoorstop disabled for Steam Play. The shortcut
  starts Valheim with Doorstop enabled for that launch only. A player still
  needs to test the installer on a Windows PC.
- The normal Steam launch stays vanilla on Mac and Windows. `Benheim.app` on
  Mac and the `Benheim` shortcut on Windows are explicit modded launch paths.
- On each managed Mac or Windows modded launch, the launcher archives the full
  `BepInEx/LogOutput.log` from the previous run before BepInEx starts. The
  launcher keeps the 10 newest archives that Benheim created and the current
  active log. If the previous run crashed, the next managed launch archives
  the leftover log. If archiving fails, the launcher shows a visible warning
  that does not block the managed launch.
- Benheim renders each structured event from one event object. It produces both
  a readable `[diag]` line and a newline-delimited JSON record. JSON records
  include a schema version, UTC timestamp, session, Benheim version, domain,
  event name, and queryable typed fields. Historical free-form diagnostics
  remain text-only until their domain needs stable fields.
- Managed launches archive `BenheimEvents.ndjson` next to the text-log archive
  for the same session. Both archive types keep the same ten newest sessions.
  The repo-owned developer query command streams these files and can find
  operations that started but did not reach a terminal event. Players do not
  need that command to play.
- Private-test installers may send typed `DiagnosticEvent` records directly to
  the configured diagnostics dataset. Each uploaded record identifies the
  current character name and connection-scoped peer. It also includes a random,
  persisted client ID, a session ID, the mod version, the exact DLL build, and
  the existing operation ID. The remote record keeps identity and common
  selectors at its root. It duplicates the operation ID there as a root
  selector. It places every producer-owned field constructed on the typed event
  under the configured Axiom `fields` map. This includes chest contents,
  identifiers, positions, revisions, item and count fields, operation and
  transaction IDs, typed errors, and paths. The serializer does not filter
  those fields. The developer query command normalizes old flat events and new
  events that use the `fields` map into the same flat result shape.

  Benheim must not construct typed events from credentials, secrets, tokens,
  passwords, raw BepInEx or Unity logs, chat, arbitrary files, or other untyped
  payloads. Those sources never enter remote diagnostics. Local NDJSON continues
  whether sharing succeeds, fails, or is disabled.
- Private-test diagnostics use one dataset-scoped ingest-only credential for
  Ben, Johnny, and Ozi. The credential is extractable from those installers.
  Never publish a private-test installer. Rotate the credential if an installer
  leaves that group or before any public release. Public packages must contain
  no diagnostics credential or config.
- `F7` remains the manual way to export the active log for sharing.
- Make each required client and server component explicit. Active players in
  our regular group must use mutually compatible Benheim versions.
- For features that depend on zone ownership, all active zone owners must use
  mutually compatible Benheim versions for that feature.
- Put Away sends each deposit to the current chest owner, and Benheim Server
  Support grants the global lease before scanning. The
  [Put Away protocol](../../shared/benheim-inventory-protocol/PROTOCOL.md) owns
  the authority, freshness, correlation, and shortcut rules.
- Defer custom persistent world objects until a specific feature needs them.
  Approve their world, recovery, migration, and removal behavior as part of
  that feature design. Store a player's manual pocket choice on an item only
  when no safer representation can preserve that choice.
- Prefer normal Valheim actions over direct inventory or world mutation.
- If Valheim rejects an action, preserve vanilla behavior or explain the local
  reason without damaging game state.
- Keep controls discoverable from the native Valheim-styled Unity menu. `Left
  Shift + B` opens or closes it.
- Benheim shortcuts and modifier actions do nothing while the player edits any
  in-game text field, including portal tags and map pin names. In Benheim's
  split-stack dialog, `Backspace`, `Delete`, and `Enter` remain active as
  text-editing controls.

## Feature Modules

| Module | Product responsibility |
| --- | --- |
| [Inventory](src/Inventory/PRODUCT.md) | Split stacks, pocket items, Put Away, and hotbar loadout swap. |
| [Production](src/Production/PRODUCT.md) | Fill stations without repetitive clicks and shorten Stone Oven baking. |
| [Repair](src/Repair/PRODUCT.md) | Batch gear repair and nearby building repair. |
| [Interaction](src/Interaction/PRODUCT.md) | Less fussy interaction and station range. |
| [Portals](src/Portals/PRODUCT.md) | Faster transitions after the destination is ready. |
| [Mining](src/Mining/PRODUCT.md) | Skill-based mining damage, crits, and AOE. |
| [Woodcutting](src/Woodcutting/PRODUCT.md) | Skill-based cleave for trees and logs. |
| [Player Combat](src/PlayerCombat/PRODUCT.md) | Reward skilled and bold play with adrenaline and earned combat states. |
| [Adrenaline](src/Adrenaline/PRODUCT.md) | Adrenaline gain, perfect-defense feedback, and decay timing. |
| [Archery](src/Archery/PRODUCT.md) | Global arrow headshots and collision-time feedback. |
| [Farming](src/Farming/PRODUCT.md) | Mass harvesting and Cultivator grid planting. |
| [Spawning](src/Spawning/PRODUCT.md) | Adjust spawn opportunities for selected native creatures. |
| [Enemy Tiers](src/EnemyTiers/PRODUCT.md) | Extend native stars and creature behavior with coherent mechanical and AI variation. |
| [Affinities](src/Affinities/PRODUCT.md) | Create weapon variation through existing combat properties and meaningful tradeoffs. |
| [Weapon Rhythm](src/WeaponRhythm/PRODUCT.md) | Reward weapon mastery through timing, charge, cadence, spacing, and existing animations. |
| [Combat Feedback](src/CombatFeedback/PRODUCT.md) | Add local bow focus and restrained camera response to existing Benheim outcomes. |
| [Shortcuts](src/Shortcuts/PRODUCT.md) | In-game discovery of controls and passive features. |

`Infrastructure` contains shared implementation support and has no independent
player-facing promise.

## Current Behavior

Benheim `0.1.52` is the current stable client. Ben gameplay-tested this combined
client and accepted it as stable. That session confirms only the behavior Ben
exercised. It does not prove feature-specific multiplayer, ownership,
installer, or rare failure paths that the session did not exercise.

Players have confirmed installation on Mac and Windows, the native menu,
diagnostic export, Mass Repair, and doubled adrenaline with its native decay
delay. Ben confirmed global Bow headshots on a Berserker and a Shaman in
`0.1.49`. Ben's multiplayer review accepted Put Away's owner-authoritative
durability. The review covered repeated use, both assignments of requester and
current chest owner, simultaneous contention followed by reuse, and exact
accepted/refunded settlement. The Inventory module owns the remaining
presentation and performance gates.

Private typed-event delivery works for Benaldson, JayTrain, and GlIzZy.
Axiom queries can select their events by player, client, session, event, and
Put Away operation. When remote sharing is unavailable or disabled, local
readable logs and `BenheimEvents.ndjson` remain available for diagnostics.

## In Development

Features listed under **In Development** in the module documents still need
gameplay proof or fixes.

Benheim `0.1.67` is the current private-test candidate. It keeps the Kill
Attribution V3 capability boundary and the 6/12 kill-chain thresholds from
`0.1.66`. Server-confirmed qualifying hostile kills now advance the shared,
untimed UNTOUCHABLE streak. Bounded typed telemetry records the actual payloads
for CLUTCH, UNTOUCHABLE, BERSERKER, and SLAUGHTERHOUSE.

The candidate also changes the Cultivator's centered grid from 5x5 to 9x9.
Perfect Impact now qualifies at the first `Character` contact authored for the
attack. This replaces the attack-start check that failed in testing. Put Away
emits bounded timing data for scanning, routing, owner mutation, requester
settlement, and the whole batch. The timing data does not change its protocol
or gameplay.

The candidate includes earned combat states, collider inspection, private
typed diagnostics, and the other changes described below. Existing adrenaline
behavior is unchanged.

Put Away and BERSERKER/SLAUGHTERHOUSE require the matching Server Support
candidate. Server Support `0.1.5` keeps lease generation `v2`, transaction
generation `v4`, and Kill Attribution V3. Its binary changes because it shares
the new Put Away timing telemetry. The client-only runtime catalog commands do
not require Server Support. See the root [Gameplay
Breakdown](../../PRODUCT.md#gameplay-breakdown) for the exact client and server
versions and compatibility boundary.

A live test still must prove runtime catalog command visibility and local
snapshot cleanup.

Configured private-test builds start with sharing enabled, and the first run
explains what the build shares. A live test still must prove that turning
sharing off stops remote forwarding while local diagnostics continue. A
one-time migration enables
sharing for legacy private-test configurations that still use the earlier
disabled default. After the migration, the `Share Diagnostics` toggle in
`Left Shift + B` persists the player's choice and stops remote forwarding
immediately when turned off. Public and unconfigured builds receive no remote
credentials. Local diagnostics, including `BenheimEvents.ndjson`, remain
enabled.

Remote schema 2 keeps identity and common selectors at the dataset root and
duplicates the operation ID there as a root selector. It puts every
producer-owned gameplay field under the configured Axiom `fields` map. Live
provider proof ingested one schema 2 event with 300 newly named producer-owned
fields. After Axiom added the `fields` map, ingesting the event left the dataset
schema at 257 entries: 256 prior fields plus the map. An APL query returned one
nested key successfully. The developer query command normalized the map-backed
event to the same flat result shape as old flat events.

All Benheim feedback that belongs in the top-left area shares one Benheim-owned
lane directly beneath the live hotbar. The lane carries grouped receipts for
Put Away and Mass Repair, pocket and unpocket confirmations, and Put Away's
already-in-progress message. It never moves to the hotbar's right side or into
a separate right-side column. When Valheim shows visible native top-left status
text, the lane moves farther down to avoid overlap while staying directly
beneath the live hotbar and on screen. It never intercepts or restyles
Valheim's messages. Short confirmations do not replace an active Put Away or
Mass Repair grouped receipt. Center messages and world feedback keep their
existing UI locations. This unified lane still needs gameplay proof.

If Benheim cannot attach a required gameplay hook, it disables all Benheim
gameplay actions. It logs the exact failure and a `[diag][Health]` event. It
keeps the problem visible in the menu Warnings block and shows one prominent
message per session that directs the player to `Left Shift + B`. If only
keybind inspection fails, Benheim reports the problem in logs and Warnings
without interrupting unrelated gameplay.

## Later

- Craft from nearby containers with ingredient totals.
- Make mining progression configurable.
- Decide whether batch crafting should go beyond Valheim's native controls.
- Food or rested HUD only after its gameplay value justifies more UI.
