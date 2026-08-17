# Adrenaline research

Adrenaline has three separate design questions: whether the meter is useful,
which actions demonstrate skill, and when succeeding is bold or risky. The
[product document](PRODUCT.md) owns the combination Benheim chooses. This note
keeps the available levers and their limits visible while that choice remains
open.

## Evidence baseline

This note uses Valheim source evidence from version `0.221.12`. The inspected
`assembly_valheim.dll` had SHA-256:

```text
ada4bd74e926680f7c17e1275b344ad2a9afd5759bf52146ad1a4c7866721b48
```

[`PROMPT.md`](../../../../PROMPT.md) owns the source-inspection commands. A
different assembly hash requires revalidation before implementation.

## Base-system levers

`Player.AddAdrenaline()` is Valheim's final entry point for adrenaline changes.
The current Benheim prefix, a hook that runs before this method, doubles every
positive grant before Valheim applies its rate, current-fill curve, status
modifiers, and meter cap. Negative changes remain native.

Benheim can tune ordinary gains, gains from specific actions, the delay before
decay, and decay speed. Meter capacity, spending, and full-meter behavior are
also possible product levers, but their technical seams have not been audited
for this redesign. They should remain native unless gain and decay tuning fail
to make the resource useful.

## Skill signals

Benheim already recognizes perfect parries, perfect dodges, Perfect Impact,
and headshots. Valheim also awards adrenaline for routine hits, blocks,
staggering an enemy, and Guardian Power. The current hook at
`Player.AddAdrenaline()` receives the grant amount but not its source. Different
rewards therefore need a narrow context at each chosen action that identifies
the source.

Direct Player kills can become another exact signal for ordinary melee,
projectile, and native area damage. The target's current owner must report the
lethal health transition to the dedicated server, which can then own chain
timing and server-wide events. Native data does not preserve human credit for
damage-over-time deaths, tames, turrets, traps, or environmental kills. It also
does not preserve the contribution history needed for assists or honest kill
steals.

## Risk and boldness signals

Current and maximum health are available on the local Player. This supports an
injured or critical-health reward without reconstructing hypothetical damage.

Nearby hostile AI exposes distance, faction hostility, and a networked alerted
state. Counting nearby alerted enemies is therefore simple and consistent
across clients. It measures nearby combat pressure, not the exact number of
enemies targeting this Player. The exact target reference lives with the AI
owner and is not replicated as a usable per-enemy Player reference.

A perfect parry retains useful blocked-damage information. A perfect dodge
reports that contact was avoided, but its reward callback does not retain the
incoming `HitData`. Benheim can tell whether a parry blocked damage, but not
whether a dodge avoided lethal damage. Treating a perfect parry or dodge at
critical health as `CLUTCH` uses the same observable rule for both and does not
claim that the avoided hit would have been lethal.

Recent confirmed kills and kill timing can represent sustained aggression once
direct kill attribution exists. A later assist or kill-steal mechanic would
need a separate contribution model and product rule.

## Output levers

Each chosen action can change its adrenaline award before Valheim applies it to
the meter. Exceptional sequences can instead apply an earned combat state. These
states are separate outputs, not another meter and not additional adrenaline.

Valheim's `SE_Stats` status effects already support the main proposed bonuses:

- skill-specific or all-skill outgoing damage;
- incoming damage resistance and armor;
- health over time and health regeneration;
- stamina regeneration and reduced attack, block, dodge, run, or jump costs;
- movement, jumping, fall damage, stagger, timed blocks, stealth, and noise;
- an icon, tooltip, duration, and start or stop effects.

An equipped item can also assign a status effect that activates when adrenaline
fills. Valheim empties the meter after it activates that effect and refreshes an
already active effect when the meter fills again. This is the native charm
output path. Earned combat states can apply a status effect directly and do not
need to consume the meter.

Refreshing an `SE_Stats` effect repeats its up-front health, stamina, eitr, and
adrenaline grants. A refreshable kill-chain state should therefore use ongoing
modifiers unless repeated up-front grants are an explicit product decision.

## Earned combat state feasibility

`UNTOUCHABLE` can build from Benheim's existing perfect-parry and perfect-dodge
signals. An accepted local-player damage event can clear it. A status effect can
provide the damage bonus, while a small Benheim-owned streak tracks escalation.
The product rule that the earned bonus lasts until the next hit requires cleanup
on death and player teardown rather than a normal duration.

`CLUTCH` can use native health-over-time or health-regeneration output. The
trigger remains the difficult part. Perfect parries retain blocked-damage
information, but the perfect-dodge reward callback does not retain the avoided
hit. Critical-health perfect defense is the current symmetric observable rule.
Literal prevented-death detection for both defenses needs a separate source
audit before implementation.

`BERSERKER` and `SLAUGHTERHOUSE!` can use the proposed direct-kill attribution
path. The current target owner reports an accepted lethal Player hit to the
dedicated server. The server can own the chain clock and escalation, then notify
the killer's client to apply the visible status. Native status effects provide
damage resistance and stamina regeneration. Damage-over-time, tame, turret,
trap, environmental, assist, and kill-steal attribution remain outside the
supported first slice.

Valheim resolves a status-effect hash through `ObjectDB.m_StatusEffects`, clones
the registered effect into the target's `SEMan`, and applies it on the target's
current owner. A Benheim-authored effect can use this path without persistent
item or world data. Every possible Player owner must register the same effect
hash. Benheim already requires the client mod for these player mechanics, while
the server only needs to report canonical kill-chain state.

Source inspection proves the registration and ownership paths. Benheim still
needs a runtime test for duplicate-safe `ObjectDB` registration, icon and effect
reuse, player teardown, and correct removal after ownership or world changes.

Sound, shake, large text, and similar presentation remain choices rather than
independent reward rules.

The design does not need every available signal. The next product decision is
which small combination makes routine adrenaline useful while paying more for
skill and meaningful risk.
