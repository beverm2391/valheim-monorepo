# Player Combat research

Player Combat has three design questions:

- Which actions demonstrate skill?
- When is a successful action bold or risky?
- Which reward helps the player continue skilled, bold combat?

The [product document](PRODUCT.md) owns the combination Benheim chooses.
[Adrenaline research](../Adrenaline/RESEARCH.md) owns the native meter and
full-meter charm paths.

## Evidence baseline

This note uses Valheim source evidence from version `0.221.12`. The inspected
`assembly_valheim.dll` had SHA-256:

```text
ada4bd74e926680f7c17e1275b344ad2a9afd5759bf52146ad1a4c7866721b48
```

[`PROMPT.md`](../../../../PROMPT.md) owns the source-inspection commands. A
different assembly hash requires revalidation before implementation.

## Skill signals

Benheim already recognizes perfect parries, perfect dodges, Perfect Impact, and
headshots. Kills directly attributed to the player through ordinary melee,
projectile, or native area damage can become another exact signal.

The target's current owner must report a lethal health transition to the
dedicated server. The server can then own chain timing and server-wide events.
Native data does not preserve human credit for damage-over-time deaths, tames,
turrets, traps, or environmental kills. It also does not preserve the damage
history needed for assists or honest kill steals.

## Risk and boldness signals

Current and maximum health are available on the local Player. This supports an
injured or critical-health reward without reconstructing hypothetical damage.

Nearby hostile AI exposes distance, faction hostility, and a networked alerted
state. Counting nearby alerted enemies measures nearby combat pressure. It does
not identify every enemy that currently targets this Player because that target
reference remains with the AI owner.

A perfect parry retains useful blocked-damage information. A perfect dodge
reports that contact was avoided, but its reward callback does not retain the
incoming `HitData`. Benheim can tell whether a parry blocked damage, but not
whether a dodge avoided lethal damage. Treating a perfect parry or dodge at
critical health as `CLUTCH` uses the same observable rule for both. It does not
claim that the avoided hit would have been lethal.

Recent confirmed kills and kill timing can represent sustained aggression. A
later assist or kill-steal mechanic would need a separate contribution model
and product rule.

## Status-effect output

Valheim's `SE_Stats` status effects support the proposed bonuses:

- skill-specific or all-skill outgoing damage;
- incoming damage resistance and armor;
- health over time and health regeneration;
- stamina regeneration and reduced attack, block, dodge, run, or jump costs;
- movement, jumping, fall damage, stagger, timed blocks, stealth, and noise;
- an icon, tooltip, duration, and start or stop effects.

An earned combat state can apply a status effect directly without consuming
adrenaline. Valheim resolves the effect hash through `ObjectDB.m_StatusEffects`.
It clones the effect into the target's `SEMan` and applies it on the target's
current owner. A Benheim-authored effect needs no persistent item or world data.
Every possible Player owner must register the same effect hash.

Source inspection proves the registration, ownership, and stat paths. Benheim
still needs runtime proof for:

- duplicate-safe registration;
- icon and effect reuse;
- player teardown; and
- removal after ownership or world changes.

## Earned combat state feasibility

`UNTOUCHABLE` can build from Benheim's existing perfect-parry and perfect-dodge
signals. An accepted local-player damage event can clear it. A status effect can
provide its damage bonus, while a small Benheim-owned streak tracks escalation.
The rule that the bonus lasts until the next hit requires cleanup on death and
player teardown instead of a normal duration.

`CLUTCH` can use native health-over-time or health-regeneration output. The
trigger remains the difficult part. Critical-health perfect defense is the
current symmetric observable rule. Literal prevented-death detection for both
defenses needs a separate source audit before implementation.

`BERSERKER` and `SLAUGHTERHOUSE!` can use the proposed direct-kill attribution
path. The server can own the chain clock and escalation, then notify the killer's
client to apply the visible status. Native status effects provide damage
resistance and stamina regeneration. Damage-over-time, tame, turret, trap,
environmental, assist, and kill-steal attribution remain outside the supported
first slice.

Sound, shake, large text, and similar presentation remain choices rather than
independent reward rules.
